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

    public PaymentGatewayService(AppDbContext context, IEnumerable<IPaymentStrategy> strategies)
    {
        _context = context;
        _strategies = strategies;
    }
    public async Task<ProcessPaymentRespone> ProcessPaymentAsync(ProcessPaymentRequest request, CancellationToken ct = default)
    {
        if(string.IsNullOrWhiteSpace(request.IdempotencyKey)) 
            throw new ValidationException("IdenpotencyKey không được để trống");
        
        var existingPayment = await _context.Payments 
            .Include(p => p.Membership).ThenInclude(m => m.Member).ThenInclude(mem => mem.User)
            .Include(p => p.Membership).ThenInclude(m => m.Plan)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey, ct);

        if(existingPayment != null)
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
            .Include(m => m.Member).ThenInclude(mem => mem.User)
            .FirstOrDefaultAsync(m => m.Id ==  request.MembershipId, ct)
            ?? throw new NotFoundException("Không tìm thấy thông tin gói đăng ký.");
        
        // Phân luồng Strategy (CARD, WALLET )
        var normMethod = request.Method.ToUpperInvariant().Contains("CARD") || request.Method.ToUpperInvariant().Contains("VNPAY") ? "CARD" : "WALLET";
        var strategy = _strategies.FirstOrDefault(s => s.Method == normMethod) ?? throw new ValidationException($"Phương thức thanh toán này không được hỗ trợ: {request.Method}");
        var context = new PaymentContext
        {
            MembershipId = membership.Id,
            OrderCode = $"MEM{membership.Id.ToString()[..6].ToUpper()}",
            UseSandbox = request.UseSandbox,
            IpAddress = request.IpAddr
        };
        // Thực hiện Charge (Mock hoặc lấy Linkl Sandbox)
        var gatewayResult = await strategy.ChargeAsync(membership.Plan.Price, request.MockSuccess, context, ct);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            MembershipId = membership.Id,
            Amount = membership.Plan.Price,
            Method = request.Method,
            IdempotencyKey = request.IdempotencyKey,
            Status = gatewayResult.PaymentUrl != null ? PaymentStatus.Pending : (gatewayResult.Success ? PaymentStatus.Completed : PaymentStatus.Failed),
            GatewayTxnCode = gatewayResult.TransactionRef,
            FailureReason = gatewayResult.ErrorMessage,
            CreatedAt = now,
            PaidAt = gatewayResult.Success && gatewayResult.PaymentUrl == null ? now : null  
        };

        if(payment.Status == PaymentStatus.Completed)
        {
            membership.Status = MembershipStatus.Active;
        }
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(ct);

        payment.Membership = membership;
        return new ProcessPaymentRespone
        {
            Success = gatewayResult.Success,
            IsIdempotent = false,
            MembershipId = membership.Id,
            MembershipStatus = membership.Status.ToString(),
            PaymentUrl = gatewayResult.PaymentUrl, // Link Sandbox cho FE
            Payment = MapPaymentDto(payment),
            ErrorMessage = gatewayResult.ErrorMessage
        };
    }

    private static PaymentDto MapPaymentDto (Payment p ) => new()
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