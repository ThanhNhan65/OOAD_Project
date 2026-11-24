using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Services.Payments;

public class PaymentGatewayService : IPaymentGatewayService
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IPaymentStrategy> _strategies;
    private readonly IRealtimeNotifier? _realtimeNotifier;

    public PaymentGatewayService(
        AppDbContext context,
        IEnumerable<IPaymentStrategy> strategies,
        IRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _strategies = strategies;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ProcessPaymentRespone> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ValidationException("IdempotencyKey không được để trống");

        var existingPayment = await _context.Payments
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mem => mem.User)
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey, ct);

        if (existingPayment != null)
        {
            return new ProcessPaymentRespone
            {
                Success = existingPayment.Status == PaymentStatus.Completed,
                IsIdempotent = true,
                MembershipId = existingPayment.MembershipId,
                MembershipStatus = existingPayment.Membership.Status.ToString(),
                Payment = MapPaymentDto(existingPayment)
            };
        }

        // Tìm membership cần thanh toán
        var membership = await _context.Memberships
            .Include(m => m.Plan)
            .Include(m => m.Branch)
            .Include(m => m.Member).ThenInclude(mem => mem.User)
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, ct)
            ?? throw new NotFoundException("Không tìm thấy thông tin gói đăng ký.");

        var isPrivileged = currentUserRole.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
            || currentUserRole.Equals("STAFF", StringComparison.OrdinalIgnoreCase);

        if (!isPrivileged && membership.Member?.UserId != currentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền thanh toán gói đăng ký của hội viên khác.");

        if (membership.Status != MembershipStatus.Pending)
            throw new ValidationException("Gói đăng ký này đã được thanh toán hoặc không còn ở trạng thái chờ thanh toán.");

        // Chỉ hỗ trợ VNPay và ZaloPay
        var upperMethod = request.Method.Trim().ToUpperInvariant();
        string normStrategyMethod;
        string displayMethod;
        if (upperMethod is "VNPAY" or "CARD")
        {
            normStrategyMethod = "CARD";
            displayMethod = "VNPay";
        }
        else if (upperMethod is "ZALOPAY" or "WALLET")
        {
            normStrategyMethod = "WALLET";
            displayMethod = "ZaloPay";
        }
        else
        {
            throw new ValidationException($"Hệ thống chỉ hỗ trợ thanh toán qua VNPay hoặc ZaloPay (không hỗ trợ: {request.Method}).");
        }

        var strategy = _strategies.FirstOrDefault(s => s.Method == normStrategyMethod)
            ?? throw new ValidationException($"Phương thức thanh toán này không được hỗ trợ: {request.Method}");

        var context = new PaymentContext
        {
            MembershipId = membership.Id,
            OrderCode = $"MEM{membership.Id.ToString()[..6].ToUpper()}",
            UseSandbox = request.UseSandbox,
            IpAddress = request.IpAddr
        };

        // Thực hiện Charge (Mock hoặc lấy Link Sandbox)
        var gatewayResult = await strategy.ChargeAsync(membership.Plan.Price, request.MockSuccess, context, ct);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            MembershipId = membership.Id,
            Amount = membership.Plan.Price,
            Method = displayMethod,
            IdempotencyKey = request.IdempotencyKey,
            Status = gatewayResult.PaymentUrl != null
                ? PaymentStatus.Pending
                : (gatewayResult.Success ? PaymentStatus.Completed : PaymentStatus.Failed),
            GatewayTxnCode = gatewayResult.TransactionRef,
            FailureReason = gatewayResult.ErrorMessage,
            CreatedAt = now,
            PaidAt = gatewayResult.Success && gatewayResult.PaymentUrl == null ? now : null
        };

        payment.Membership = membership;
        _context.Payments.Add(payment);

        if (payment.Status == PaymentStatus.Completed)
        {
            await CompletePaymentAndActivateMembershipAsync(payment, ct);
        }
        else
        {
            await _context.SaveChangesAsync(ct);
        }

        return new ProcessPaymentRespone
        {
            Success = gatewayResult.Success,
            IsIdempotent = false,
            MembershipId = membership.Id,
            MembershipStatus = membership.Status.ToString(),
            PaymentUrl = gatewayResult.PaymentUrl,
            Payment = MapPaymentDto(payment),
            ErrorMessage = gatewayResult.ErrorMessage
        };
    }

    public async Task<PaymentVerificationResult> VerifyVnpayReturnAsync(
        IDictionary<string, string> queryParams,
        CancellationToken ct = default)
    {
        var cardStrategy = _strategies.OfType<CardPaymentStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException("Chưa cấu hình CardPaymentStrategy cho VNPay.");

        queryParams.TryGetValue("vnp_TxnRef", out var txnRef);
        queryParams.TryGetValue("vnp_ResponseCode", out var responseCode);
        queryParams.TryGetValue("vnp_TransactionStatus", out var txnStatus);
        queryParams.TryGetValue("vnp_TransactionNo", out var vnpTxnNo);
        queryParams.TryGetValue("vnp_BankCode", out var bankCode);

        if (!cardStrategy.VerifyVnpaySignature(queryParams))
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "InvalidSignature",
                Provider = "VNPay",
                VerifiedBy = "VNPay Sandbox (HMAC-SHA512)",
                TransactionCode = txnRef,
                Message = "Chữ ký bảo mật HMAC-SHA512 từ VNPay không hợp lệ.",
                ErrorMessage = "Invalid VNPay SecureHash."
            };
        }

        if (string.IsNullOrWhiteSpace(txnRef))
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "NotFound",
                Provider = "VNPay",
                VerifiedBy = "VNPay Sandbox (HMAC-SHA512)",
                Message = "Thiếu mã giao dịch vnp_TxnRef.",
                ErrorMessage = "Missing vnp_TxnRef."
            };
        }

        var payment = await _context.Payments
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .Include(p => p.Membership).ThenInclude(m => m.Branch)
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mem => mem.User)
            .FirstOrDefaultAsync(p => p.GatewayTxnCode == txnRef, ct);

        if (payment == null)
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "NotFound",
                Provider = "VNPay",
                VerifiedBy = "VNPay Sandbox (HMAC-SHA512)",
                TransactionCode = txnRef,
                Message = "Không tìm thấy giao dịch tương ứng trong hệ thống.",
                ErrorMessage = "Payment transaction not found."
            };
        }

        if (queryParams.TryGetValue("vnp_Amount", out var vnpAmountStr)
            && long.TryParse(vnpAmountStr, out var vnpAmount)
            && vnpAmount != (long)(payment.Amount * 100))
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "InvalidAmount",
                Provider = "VNPay",
                VerifiedBy = "VNPay Sandbox (HMAC-SHA512)",
                TransactionCode = payment.GatewayTxnCode,
                Amount = payment.Amount,
                MembershipId = payment.MembershipId,
                MembershipStatus = payment.Membership.Status.ToString(),
                Message = "Số tiền xác thực từ VNPay không khớp với giá trị đơn hàng.",
                ErrorMessage = "VNPay amount mismatch."
            };
        }

        var isOk = responseCode == "00" && (string.IsNullOrEmpty(txnStatus) || txnStatus == "00");
        if (isOk)
        {
            if (payment.Status != PaymentStatus.Completed)
            {
                await CompletePaymentAndActivateMembershipAsync(payment, ct);
            }

            return new PaymentVerificationResult
            {
                Success = true,
                Status = "Completed",
                Provider = "VNPay",
                VerifiedBy = "VNPay Sandbox (HMAC-SHA512)",
                TransactionCode = payment.GatewayTxnCode,
                GatewayTransactionNo = vnpTxnNo,
                BankCode = bankCode,
                Amount = payment.Amount,
                PlanName = payment.Membership.Plan.Name,
                MembershipId = payment.MembershipId,
                MembershipStatus = payment.Membership.Status.ToString(),
                PaidAt = payment.PaidAt,
                Message = "VNPay đã xác thực chữ ký HMAC-SHA512 và xác nhận thanh toán thành công."
            };
        }

        if (payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = responseCode == "24"
                ? "Khách hàng đã hủy giao dịch trên cổng VNPay."
                : $"Thanh toán VNPay thất bại (Mã lỗi: {responseCode}).";
            await _context.SaveChangesAsync(ct);
        }

        return new PaymentVerificationResult
        {
            Success = false,
            Status = "Failed",
            Provider = "VNPay",
            VerifiedBy = "VNPay Sandbox (HMAC-SHA512)",
            TransactionCode = payment.GatewayTxnCode,
            GatewayTransactionNo = vnpTxnNo,
            BankCode = bankCode,
            Amount = payment.Amount,
            PlanName = payment.Membership.Plan.Name,
            MembershipId = payment.MembershipId,
            MembershipStatus = payment.Membership.Status.ToString(),
            Message = payment.FailureReason ?? "Giao dịch VNPay không thành công.",
            ErrorMessage = payment.FailureReason
        };
    }

    public async Task<PaymentVerificationResult> VerifyZaloPayReturnAsync(
        string appTransId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appTransId))
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "BadRequest",
                Provider = "ZaloPay",
                VerifiedBy = "ZaloPay Sandbox API /v2/query (HMAC-SHA256)",
                Message = "Thiếu mã giao dịch apptransid từ ZaloPay.",
                ErrorMessage = "Missing apptransid."
            };
        }

        var walletStrategy = _strategies.OfType<WalletPaymentStrategy>().FirstOrDefault()
            ?? throw new InvalidOperationException("Chưa cấu hình WalletPaymentStrategy cho ZaloPay.");

        var payment = await _context.Payments
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .Include(p => p.Membership).ThenInclude(m => m.Branch)
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mem => mem.User)
            .FirstOrDefaultAsync(p => p.GatewayTxnCode == appTransId, ct);

        if (payment == null)
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "NotFound",
                Provider = "ZaloPay",
                VerifiedBy = "ZaloPay Sandbox API /v2/query (HMAC-SHA256)",
                TransactionCode = appTransId,
                Message = "Không tìm thấy giao dịch ZaloPay tương ứng trong hệ thống.",
                ErrorMessage = "Payment transaction not found."
            };
        }

        var (returnCode, subCode, retMsg, zpTransId) = await walletStrategy.QueryZaloPayOrderAsync(appTransId, ct);

        if (returnCode == 1)
        {
            if (payment.Status != PaymentStatus.Completed)
            {
                await CompletePaymentAndActivateMembershipAsync(payment, ct);
            }

            return new PaymentVerificationResult
            {
                Success = true,
                Status = "Completed",
                Provider = "ZaloPay",
                VerifiedBy = "ZaloPay Sandbox API /v2/query (HMAC-SHA256)",
                TransactionCode = payment.GatewayTxnCode,
                GatewayTransactionNo = zpTransId,
                Amount = payment.Amount,
                PlanName = payment.Membership.Plan.Name,
                MembershipId = payment.MembershipId,
                MembershipStatus = payment.Membership.Status.ToString(),
                PaidAt = payment.PaidAt,
                Message = "Máy chủ ZaloPay Sandbox đã đối soát và xác nhận thanh toán thành công."
            };
        }

        if (returnCode == 3)
        {
            return new PaymentVerificationResult
            {
                Success = false,
                Status = "Pending",
                Provider = "ZaloPay",
                VerifiedBy = "ZaloPay Sandbox API /v2/query (HMAC-SHA256)",
                TransactionCode = payment.GatewayTxnCode,
                GatewayTransactionNo = zpTransId,
                Amount = payment.Amount,
                PlanName = payment.Membership.Plan.Name,
                MembershipId = payment.MembershipId,
                MembershipStatus = payment.Membership.Status.ToString(),
                Message = $"Giao dịch đang chờ xác nhận trên ZaloPay ({retMsg})."
            };
        }

        if (payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = $"ZaloPay xác nhận giao dịch thất bại hoặc đã hủy (code: {returnCode}, sub: {subCode} - {retMsg}).";
            await _context.SaveChangesAsync(ct);
        }

        return new PaymentVerificationResult
        {
            Success = false,
            Status = "Failed",
            Provider = "ZaloPay",
            VerifiedBy = "ZaloPay Sandbox API /v2/query (HMAC-SHA256)",
            TransactionCode = payment.GatewayTxnCode,
            GatewayTransactionNo = zpTransId,
            Amount = payment.Amount,
            PlanName = payment.Membership.Plan.Name,
            MembershipId = payment.MembershipId,
            MembershipStatus = payment.Membership.Status.ToString(),
            Message = payment.FailureReason ?? "Thanh toán ZaloPay không thành công.",
            ErrorMessage = payment.FailureReason
        };
    }

    public async Task<bool> HandleZaloPayCallbackAsync(string data, string mac, CancellationToken ct = default)
    {
        var walletStrategy = _strategies.OfType<WalletPaymentStrategy>().FirstOrDefault();
        if (walletStrategy == null || string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(mac))
            return false;

        if (!walletStrategy.VerifyZaloPayCallbackMac(data, mac))
            return false;

        using var doc = JsonDocument.Parse(data);
        if (!doc.RootElement.TryGetProperty("app_trans_id", out var transEl))
            return false;

        var appTransId = transEl.GetString();
        if (string.IsNullOrWhiteSpace(appTransId))
            return false;

        var payment = await _context.Payments
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .Include(p => p.Membership).ThenInclude(m => m.Branch)
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mem => mem.User)
            .FirstOrDefaultAsync(p => p.GatewayTxnCode == appTransId, ct);

        if (payment == null)
            return false;

        if (payment.Status != PaymentStatus.Completed)
        {
            await CompletePaymentAndActivateMembershipAsync(payment, ct);
        }

        return true;
    }

    private async Task CompletePaymentAndActivateMembershipAsync(Payment payment, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = now;
        payment.FailureReason = null;

        var membership = payment.Membership;
        membership.Status = MembershipStatus.Active;

        // Hủy các gói Pending cũ cùng loại bị bỏ dở của hội viên
        var targetPlanType = membership.Plan?.PlanType ?? "MEMBERSHIP";
        var stalePendings = await _context.Memberships
            .Include(m => m.Plan)
            .Where(m => m.MemberId == membership.MemberId
                     && m.Id != membership.Id
                     && m.Status == MembershipStatus.Pending
                     && m.Plan.PlanType == targetPlanType)
            .ToListAsync(ct);

        foreach (var stale in stalePendings)
        {
            stale.Status = MembershipStatus.Cancelled;
        }

        string? notifyTitle = null;
        string? notifyContent = null;
        if (membership.Member?.UserId != null)
        {
            var isPt = membership.Plan?.PlanType?.Equals("PT", StringComparison.OrdinalIgnoreCase) == true;
            var actionText = isPt
                ? "Mua gói PT"
                : membership.RenewedFromId.HasValue
                    ? "Gia hạn gói hội viên"
                    : "Đăng ký gói hội viên";

            notifyTitle = $"{actionText} thành công";
            notifyContent = $"Giao dịch {payment.Method} ({payment.GatewayTxnCode}) đã được xác thực. Gói {membership.Plan?.Name} đã kích hoạt đến {membership.EndDate:dd/MM/yyyy}.";

            _context.Notifications.Add(new Notification
            {
                UserId = membership.Member.UserId,
                Type = "PAYMENT_SUCCESS",
                Title = notifyTitle,
                Content = notifyContent,
                RefType = "Membership",
                RefId = membership.Id,
                IsRead = false,
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(ct);

        if (_realtimeNotifier != null && membership.Member?.UserId != null)
        {
            await _realtimeNotifier.NotifyUsersAsync(new[] { membership.Member.UserId }, notifyTitle, notifyContent, ct);
        }
    }

    private static PaymentDto MapPaymentDto(Payment p) => new()
    {
        Id = p.Id,
        MembershipId = p.MembershipId,
        Amount = p.Amount,
        Method = p.Method,
        Status = p.Status.ToString(),
        GatewayTxnCode = p.GatewayTxnCode,
        CreatedAt = p.CreatedAt,
        PaidAt = p.PaidAt,
        PlanName = p.Membership?.Plan?.Name ?? string.Empty,
        MemberName = p.Membership?.Member?.User?.FullName ?? string.Empty
    };
}