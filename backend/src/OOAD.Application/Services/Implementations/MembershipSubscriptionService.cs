using OOAD.Application.Common.Options;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

/// <summary>
/// Service quản lý trọn vẹn vòng đời Gói tập (MembershipPlans), Đăng ký/Gia hạn (Memberships),
/// Thanh toán (Payments) và Yêu cầu tạm ngưng (FreezeRequests).
/// </summary>
public class MembershipSubscriptionService : IMembershipSubscriptionService
{
    private readonly IMembershipPlanRepository _membershipPlanRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IFreezeRequestRepository _freezeRequestRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly IClassSessionRepository _classSessionRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BusinessRuleOptions _ruleOptions;

    public MembershipSubscriptionService(
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipRepository membershipRepository,
        IPaymentRepository paymentRepository,
        IFreezeRequestRepository freezeRequestRepository,
        IMemberRepository memberRepository,
        IStaffRepository staffRepository,
        IBranchRepository branchRepository,
        IBookingRepository bookingRepository,
        IWaitlistEntryRepository waitlistEntryRepository,
        IClassSessionRepository classSessionRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        BusinessRuleOptions ruleOptions)
    {
        _membershipPlanRepository = membershipPlanRepository;
        _membershipRepository = membershipRepository;
        _paymentRepository = paymentRepository;
        _freezeRequestRepository = freezeRequestRepository;
        _memberRepository = memberRepository;
        _staffRepository = staffRepository;
        _branchRepository = branchRepository;
        _bookingRepository = bookingRepository;
        _waitlistEntryRepository = waitlistEntryRepository;
        _classSessionRepository = classSessionRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _ruleOptions = ruleOptions;
    }

    private int GetWaitlistCutoffHours() =>
        _ruleOptions.WaitlistCutoffHours > 0 ? _ruleOptions.WaitlistCutoffHours : 8;

    // =========================================================================
    // 1. DANH MỤC GÓI TẬP (MEMBERSHIP PLANS)
    // =========================================================================

    public async Task<IReadOnlyList<MembershipPlanDto>> GetAllPlansAsync(
        string? planType = null,
        CancellationToken cancellationToken = default)
    {
        var plans = await _membershipPlanRepository.GetAllWithBranchesAsync(planType, cancellationToken);
        return plans.Select(MapPlanDto).ToList();
    }

    public async Task<MembershipPlanDto?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _membershipPlanRepository.GetByIdWithBranchesAsync(id, asNoTracking: true, cancellationToken);
        return plan == null ? null : MapPlanDto(plan);
    }

    public async Task<MembershipPlanDto> CreatePlanAsync(
        CreateUpdateMembershipPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePlanRequest(request);

        var normalizedType = NormalizePlanType(request.PlanType);
        var plan = new MembershipPlan
        {
            Name = request.Name.Trim(),
            DurationDays = request.DurationDays,
            Price = request.Price,
            PriorityLevel = normalizedType == "PT" ? 1 : Math.Max(1, request.PriorityLevel),
            MaxBookingsPerMonth = normalizedType == "PT" ? 0 : Math.Max(0, request.MaxBookingsPerMonth),
            MaxFreezeTimes = normalizedType == "PT" ? 0 : Math.Max(0, request.MaxFreezeTimes),
            MaxFreezeDays = normalizedType == "PT" ? 0 : Math.Max(0, request.MaxFreezeDays),
            IsAllBranches = request.IsAllBranches,
            PlanType = normalizedType,
            PtSessions = Math.Max(0, request.PtSessions)
        };

        if (!request.IsAllBranches)
        {
            var validBranchIds = await ResolveValidBranchIdsAsync(request.BranchIds, cancellationToken);
            foreach (var bid in validBranchIds)
            {
                plan.PlanBranches.Add(new PlanBranch
                {
                    PlanId = plan.Id,
                    BranchId = bid
                });
            }
        }

        await _membershipPlanRepository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _membershipPlanRepository.GetByIdWithBranchesAsync(plan.Id, asNoTracking: true, cancellationToken)
            ?? plan;

        return MapPlanDto(saved);
    }

    public async Task<MembershipPlanDto> UpdatePlanAsync(
        Guid id,
        CreateUpdateMembershipPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePlanRequest(request);

        var plan = await _membershipPlanRepository.GetByIdWithBranchesAsync(id, asNoTracking: false, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy gói tập với mã: {id}");

        var normalizedType = NormalizePlanType(request.PlanType);
        plan.Name = request.Name.Trim();
        plan.DurationDays = request.DurationDays;
        plan.Price = request.Price;
        plan.PriorityLevel = normalizedType == "PT" ? 1 : Math.Max(1, request.PriorityLevel);
        plan.MaxBookingsPerMonth = normalizedType == "PT" ? 0 : Math.Max(0, request.MaxBookingsPerMonth);
        plan.MaxFreezeTimes = normalizedType == "PT" ? 0 : Math.Max(0, request.MaxFreezeTimes);
        plan.MaxFreezeDays = normalizedType == "PT" ? 0 : Math.Max(0, request.MaxFreezeDays);
        plan.IsAllBranches = request.IsAllBranches;
        plan.PlanType = normalizedType;
        plan.PtSessions = Math.Max(0, request.PtSessions);

        // Đồng bộ lại PlanBranches
        if (plan.PlanBranches.Count > 0)
        {
            _membershipPlanRepository.RemovePlanBranches(plan.PlanBranches);
        }

        if (!request.IsAllBranches)
        {
            var validBranchIds = await ResolveValidBranchIdsAsync(request.BranchIds, cancellationToken);
            foreach (var bid in validBranchIds)
            {
                await _membershipPlanRepository.AddPlanBranchAsync(new PlanBranch
                {
                    PlanId = plan.Id,
                    BranchId = bid
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _membershipPlanRepository.GetByIdWithBranchesAsync(plan.Id, asNoTracking: true, cancellationToken)
            ?? plan;

        return MapPlanDto(saved);
    }

    public async Task DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _membershipPlanRepository.GetByIdWithMembershipsAndBranchesAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy gói tập với mã: {id}");

        if (plan.Memberships.Count > 0)
            throw new ValidationException($"Không thể xóa gói '{plan.Name}' vì đã có hội viên đăng ký sử dụng.");

        if (plan.PlanBranches.Count > 0)
            _membershipPlanRepository.RemovePlanBranches(plan.PlanBranches);

        _membershipPlanRepository.Delete(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // =========================================================================
    // 2. ĐĂNG KÝ / GIA HẠN GÓI TẬP & THANH TOÁN (MEMBERSHIPS & PAYMENTS)
    // =========================================================================

    public async Task<IReadOnlyList<MembershipDto>> GetMyMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null) return Array.Empty<MembershipDto>();

        var memberships = await _membershipRepository.GetByMemberIdFullDetailsAsync(member.Id, cancellationToken);
        return memberships.Select(m => MapMembershipDto(m, member.User.FullName)).ToList();
    }

    public async Task<(MembershipDto Membership, PaymentDto Payment)> PurchaseAsync(
        Guid userId,
        PurchaseMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ hội viên cho tài khoản hiện tại.");

        var plan = await _membershipPlanRepository.GetByIdWithBranchesAsync(request.PlanId, asNoTracking: true, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy gói tập đã chọn.");

        var branch = await ResolvePurchaseBranchAsync(plan, request.BranchId, cancellationToken);
        var todayVn = SchedulingHelpers.ToVietnamDate(DateTime.UtcNow);
        var nowUtc = DateTime.UtcNow;

        var latestValidGymMembership = await _membershipRepository.GetLatestValidGymMembershipAsync(
            member.Id,
            todayVn,
            excludeMembershipId: null,
            cancellationToken);

        DateTime startDate;
        DateTime endDate;
        Guid? renewedFromId = null;

        if (plan.PlanType.Equals("PT", StringComparison.OrdinalIgnoreCase))
        {
            if (latestValidGymMembership == null)
                throw new ValidationException("Bạn cần có gói hội viên còn hiệu lực để mua gói PT 1-kèm-1.");

            startDate = todayVn;
            endDate = todayVn.AddDays(Math.Max(1, plan.DurationDays));
        }
        else
        {
            if (latestValidGymMembership != null)
            {
                renewedFromId = latestValidGymMembership.Id;
                startDate = latestValidGymMembership.EndDate.Date.AddDays(1);
                endDate = startDate.AddDays(Math.Max(1, plan.DurationDays));
            }
            else
            {
                startDate = todayVn;
                endDate = todayVn.AddDays(Math.Max(1, plan.DurationDays));
            }
        }

        var membership = new Membership
        {
            MemberId = member.Id,
            PlanId = plan.Id,
            RenewedFromId = renewedFromId,
            BranchId = branch?.Id,
            StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc),
            RemainingSessions = Math.Max(0, plan.PtSessions),
            Status = request.SimulateSuccess ? MembershipStatus.Active : MembershipStatus.Pending
        };

        var method = NormalizePaymentMethod(request.PaymentMethod);
        var payment = new Payment
        {
            MembershipId = membership.Id,
            Amount = plan.Price,
            Method = method,
            Status = request.SimulateSuccess ? PaymentStatus.Completed : PaymentStatus.Failed,
            GatewayTxnCode = $"TXN-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            CreatedAt = nowUtc,
            PaidAt = request.SimulateSuccess ? nowUtc : null
        };

        await _membershipRepository.AddAsync(membership, cancellationToken);
        await _paymentRepository.AddAsync(payment, cancellationToken);

        if (request.SimulateSuccess)
        {
            var isPt = plan.PlanType.Equals("PT", StringComparison.OrdinalIgnoreCase);
            var actionText = isPt ? "Mua gói PT" : renewedFromId.HasValue ? "Gia hạn gói hội viên" : "Đăng ký gói hội viên";
            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                member.UserId,
                "PAYMENT_SUCCESS",
                $"{actionText} thành công",
                $"Gói {plan.Name} đã được kích hoạt đến {endDate:dd/MM/yyyy} tại {branch?.Name ?? "toàn hệ thống"}.",
                "Membership",
                membership.Id), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        membership.Plan = plan;
        membership.Branch = branch;
        payment.Membership = membership;

        return (MapMembershipDto(membership, member.User.FullName), MapPaymentDto(payment, member.User.FullName));
    }

    public async Task<(MembershipDto Membership, PaymentDto Payment)> RetryPaymentAsync(
        Guid userId,
        Guid membershipId,
        RetryPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ hội viên.");

        var membership = await _membershipRepository.GetByIdAndMemberIdWithDetailsAsync(membershipId, member.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy gói đăng ký cần thanh toán lại.");

        if (membership.Status != MembershipStatus.Pending)
            throw new ValidationException("Gói đăng ký này đã được thanh toán hoặc không còn ở trạng thái chờ thanh toán.");

        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            var branch = await ResolvePurchaseBranchAsync(membership.Plan, request.BranchId, cancellationToken);
            if (branch != null)
            {
                membership.BranchId = branch.Id;
                membership.Branch = branch;
            }
        }

        var todayVn = SchedulingHelpers.ToVietnamDate(DateTime.UtcNow);
        var nowUtc = DateTime.UtcNow;

        if (request.SimulateSuccess)
        {
            var latestValidGymMembership = await _membershipRepository.GetLatestValidGymMembershipAsync(
                member.Id,
                todayVn,
                excludeMembershipId: membership.Id,
                cancellationToken);

            DateTime startDate;
            DateTime endDate;
            if (!membership.Plan.PlanType.Equals("PT", StringComparison.OrdinalIgnoreCase) && latestValidGymMembership != null)
            {
                membership.RenewedFromId = latestValidGymMembership.Id;
                startDate = latestValidGymMembership.EndDate.Date.AddDays(1);
                endDate = startDate.AddDays(Math.Max(1, membership.Plan.DurationDays));
            }
            else
            {
                startDate = todayVn;
                endDate = todayVn.AddDays(Math.Max(1, membership.Plan.DurationDays));
            }

            membership.StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            membership.EndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            membership.Status = MembershipStatus.Active;
        }

        var method = NormalizePaymentMethod(request.PaymentMethod);
        var payment = new Payment
        {
            MembershipId = membership.Id,
            Amount = membership.Plan.Price,
            Method = method,
            Status = request.SimulateSuccess ? PaymentStatus.Completed : PaymentStatus.Failed,
            GatewayTxnCode = $"TXN-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            CreatedAt = nowUtc,
            PaidAt = request.SimulateSuccess ? nowUtc : null
        };

        await _paymentRepository.AddAsync(payment, cancellationToken);

        if (request.SimulateSuccess)
        {
            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                member.UserId,
                "PAYMENT_SUCCESS",
                "Thanh toán gói tập thành công",
                $"Gói {membership.Plan.Name} đã được kích hoạt đến {membership.EndDate:dd/MM/yyyy}.",
                "Membership",
                membership.Id), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        payment.Membership = membership;
        return (MapMembershipDto(membership, member.User.FullName), MapPaymentDto(payment, member.User.FullName));
    }

    public async Task<IReadOnlyList<PaymentDto>> GetMyPaymentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null) return Array.Empty<PaymentDto>();

        var payments = await _paymentRepository.GetByMemberIdWithDetailsAsync(member.Id, cancellationToken);
        return payments.Select(p => MapPaymentDto(p, member.User.FullName)).ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> GetAllPaymentsAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository.GetAllWithDetailsAsync(branchId, take: 300, cancellationToken);
        return payments.Select(p => MapPaymentDto(p, p.Membership.Member?.User?.FullName ?? "Hội viên")).ToList();
    }

    // =========================================================================
    // 3. YÊU CẦU TẠM NGƯNG GÓI (FREEZE REQUESTS)
    // =========================================================================

    public async Task<IReadOnlyList<FreezeRequestDto>> GetMyFreezeRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null) return Array.Empty<FreezeRequestDto>();

        var list = await _freezeRequestRepository.GetByMemberIdWithDetailsAsync(member.Id, cancellationToken);
        return list.Select(f => MapFreezeDto(f, member.User.FullName, member.User.Phone, 0)).ToList();
    }

    public async Task<IReadOnlyList<FreezeRequestDto>> GetAllFreezeRequestsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        FreezeRequestStatus? filterStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<FreezeRequestStatus>(status, true, out var parsedStatus))
                filterStatus = parsedStatus;
        }

        var list = await _freezeRequestRepository.GetAllWithDetailsAsync(filterStatus, cancellationToken);

        // Tính trước số lịch lớp / PT sẽ tự hủy nếu duyệt cho các đơn Pending
        var pendingMemberIds = list
            .Where(f => f.Status == FreezeRequestStatus.Pending)
            .Select(f => f.Membership.MemberId)
            .Distinct()
            .ToList();

        var bookingsByMember = new Dictionary<Guid, List<Booking>>();
        var waitlistsByMember = new Dictionary<Guid, List<WaitlistEntry>>();

        if (pendingMemberIds.Count > 0)
        {
            var activeBookings = await _bookingRepository.GetActiveByMemberIdsWithDetailsAsync(
                pendingMemberIds,
                asNoTracking: true,
                cancellationToken);

            bookingsByMember = activeBookings.GroupBy(b => b.MemberId).ToDictionary(g => g.Key, g => g.ToList());

            var activeWaitlists = await _waitlistEntryRepository.GetWaitingByMemberIdsWithSessionAsync(
                pendingMemberIds,
                asNoTracking: true,
                cancellationToken);

            waitlistsByMember = activeWaitlists.GroupBy(w => w.MemberId).ToDictionary(g => g.Key, g => g.ToList());
        }

        var results = new List<FreezeRequestDto>(list.Count);
        foreach (var fr in list)
        {
            int affected = 0;
            if (fr.Status == FreezeRequestStatus.Pending)
            {
                var fromDate = fr.FromDate.Date;
                var toDate = fr.ToDate.Date;
                if (bookingsByMember.TryGetValue(fr.Membership.MemberId, out var mBookings))
                {
                    affected += mBookings.Count(b =>
                    {
                        var dt = b.Type == BookingType.Class ? b.ClassSession?.StartAt : b.Availability?.StartAt;
                        if (dt == null) return false;
                        var vnDate = SchedulingHelpers.ToVietnamDate(dt.Value);
                        return vnDate >= fromDate && vnDate <= toDate;
                    });
                }
                if (waitlistsByMember.TryGetValue(fr.Membership.MemberId, out var mWaitlists))
                {
                    affected += mWaitlists.Count(w =>
                    {
                        if (w.ClassSession == null) return false;
                        var vnDate = SchedulingHelpers.ToVietnamDate(w.ClassSession.StartAt);
                        return vnDate >= fromDate && vnDate <= toDate;
                    });
                }
            }

            results.Add(MapFreezeDto(
                fr,
                fr.Membership.Member?.User?.FullName ?? "Hội viên",
                fr.Membership.Member?.User?.Phone,
                affected));
        }

        return results;
    }

    public async Task<FreezeRequestDto> CreateFreezeRequestAsync(
        Guid userId,
        CreateFreezeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Vui lòng nhập lý do tạm ngưng gói.");
        if (request.Days <= 0)
            throw new ValidationException("Số ngày tạm ngưng phải lớn hơn 0.");

        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ hội viên.");

        var membership = await _membershipRepository.GetByIdAndMemberIdWithDetailsAsync(request.MembershipId, member.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy gói hội viên.");

        if (!membership.Plan.PlanType.Equals("MEMBERSHIP", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Gói PT 1-kèm-1 không áp dụng chính sách tạm ngưng (Freeze).");

        if (membership.Status != MembershipStatus.Active && membership.Status != MembershipStatus.Frozen)
            throw new ValidationException("Chỉ có thể yêu cầu tạm ngưng cho gói hội viên đang hiệu lực.");

        if (membership.Plan.MaxFreezeTimes <= 0 || membership.Plan.MaxFreezeDays <= 0)
            throw new ValidationException($"Gói '{membership.Plan.Name}' không hỗ trợ tạm ngưng (Freeze).");

        if (membership.FreezeRequests.Any(f => f.Status == FreezeRequestStatus.Pending))
            throw new ValidationException("Gói tập này đang có một yêu cầu tạm ngưng chờ lễ tân duyệt.");

        var approved = membership.FreezeRequests.Where(f => f.Status == FreezeRequestStatus.Approved).ToList();
        var usedTimes = approved.Count;
        var usedDays = approved.Sum(f => f.Days);

        if (usedTimes >= membership.Plan.MaxFreezeTimes)
            throw new ValidationException($"Bạn đã sử dụng hết {membership.Plan.MaxFreezeTimes} lần tạm ngưng cho phép của gói.");

        var remainDays = membership.Plan.MaxFreezeDays - usedDays;
        if (request.Days > remainDays)
            throw new ValidationException($"Số ngày tạm ngưng vượt quá hạn mức còn lại ({remainDays} ngày).");

        var todayVn = SchedulingHelpers.ToVietnamDate(DateTime.UtcNow);
        var fromDate = request.FromDate.Date;
        if (fromDate <= todayVn)
            throw new ValidationException("Ngày bắt đầu tạm ngưng phải từ ngày mai trở đi.");

        if (fromDate > membership.EndDate.Date)
            throw new ValidationException("Ngày bắt đầu tạm ngưng không thể sau ngày hết hạn gói.");

        var toDate = fromDate.AddDays(request.Days - 1);

        var freeze = new FreezeRequest
        {
            MembershipId = membership.Id,
            FromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc),
            ToDate = DateTime.SpecifyKind(toDate, DateTimeKind.Utc),
            Days = request.Days,
            Reason = request.Reason.Trim(),
            Status = FreezeRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _freezeRequestRepository.AddAsync(freeze, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        freeze.Membership = membership;
        return MapFreezeDto(freeze, member.User.FullName, member.User.Phone, 0);
    }

    public async Task<FreezeRequestDto> HandleFreezeRequestAsync(
        Guid staffUserId,
        Guid freezeRequestId,
        HandleFreezeRequest request,
        CancellationToken cancellationToken = default)
    {
        var freeze = await _freezeRequestRepository.GetByIdWithFullDetailsAsync(freezeRequestId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu tạm ngưng.");

        if (freeze.Status != FreezeRequestStatus.Pending)
            throw new ValidationException("Yêu cầu tạm ngưng này đã được xử lý trước đó.");

        // Lưu Staff.Id (KHÔNG lưu User.Id) để đảm bảo đúng khóa ngoại Freeze_Requests.Handled_By -> Staff.Id
        var staff = await _staffRepository.GetByUserIdWithUserAsync(staffUserId, cancellationToken);

        freeze.HandledBy = staff?.Id;
        freeze.Handler = staff;

        var member = freeze.Membership.Member;
        var memberUserId = member.UserId;
        var noteSuffix = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" Lý do/Ghi chú: {request.Note.Trim()}";

        if (!request.Approve)
        {
            freeze.Status = FreezeRequestStatus.Rejected;
            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                memberUserId,
                "FREEZE_REJECTED",
                "Yêu cầu tạm ngưng gói bị từ chối",
                $"Yêu cầu tạm ngưng {freeze.Days} ngày ({freeze.FromDate:dd/MM/yyyy} – {freeze.ToDate:dd/MM/yyyy}) cho gói {freeze.Membership.Plan.Name} chưa được duyệt.{noteSuffix}",
                "FreezeRequest",
                freeze.Id), cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return MapFreezeDto(freeze, member.User.FullName, member.User.Phone, 0);
        }

        // Duyệt Freeze -> Cộng thêm ngày vào hạn gói + cập nhật trạng thái nếu đang hiệu lực
        freeze.Status = FreezeRequestStatus.Approved;
        freeze.Membership.EndDate = freeze.Membership.EndDate.AddDays(freeze.Days);

        var todayVn = SchedulingHelpers.ToVietnamDate(DateTime.UtcNow);
        var fromDate = freeze.FromDate.Date;
        var toDate = freeze.ToDate.Date;

        if (fromDate <= todayVn && todayVn <= toDate)
        {
            freeze.Membership.Status = MembershipStatus.Frozen;
        }

        // Tự động hủy toàn bộ Bookings (Lớp & PT) rơi vào khoảng [fromDate..toDate]
        var memberBookings = await _bookingRepository.GetActiveByMemberIdsWithDetailsAsync(
            new[] { member.Id },
            asNoTracking: false,
            cancellationToken);

        var affectedClassSessionIds = new HashSet<Guid>();
        var autoCancelledCount = 0;

        foreach (var booking in memberBookings)
        {
            var startAt = booking.Type == BookingType.Class
                ? booking.ClassSession?.StartAt
                : booking.Availability?.StartAt;

            if (startAt == null) continue;
            var vnDate = SchedulingHelpers.ToVietnamDate(startAt.Value);
            if (vnDate < fromDate || vnDate > toDate) continue;

            booking.Status = BookingStatus.AutoCancelled;
            booking.CancelledAt = DateTime.UtcNow;
            autoCancelledCount++;

            if (booking.Type == BookingType.PersonalTrainer)
            {
                if (booking.Availability != null)
                    booking.Availability.IsBooked = false;
                if (booking.Membership != null)
                    booking.Membership.RemainingSessions += 1;
            }
            else if (booking.ClassSessionId.HasValue)
            {
                affectedClassSessionIds.Add(booking.ClassSessionId.Value);
            }
        }

        // Hủy các WaitlistEntry rơi vào khoảng Freeze
        var memberWaitlists = await _waitlistEntryRepository.GetWaitingByMemberIdsWithSessionAsync(
            new[] { member.Id },
            asNoTracking: false,
            cancellationToken);

        foreach (var w in memberWaitlists)
        {
            if (w.ClassSession == null) continue;
            var vnDate = SchedulingHelpers.ToVietnamDate(w.ClassSession.StartAt);
            if (vnDate >= fromDate && vnDate <= toDate)
            {
                w.Status = "CANCELLED";
                autoCancelledCount++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Đôn Waitlist lên chỗ trống cho các buổi lớp nhóm vừa tự hủy (nếu còn > T-cutoff)
        if (affectedClassSessionIds.Count > 0)
        {
            await PromoteWaitlistsAfterFreezeAsync(affectedClassSessionIds, cancellationToken);
        }

        var extraMsg = autoCancelledCount > 0
            ? $" {autoCancelledCount} lịch tập trong thời gian tạm ngưng đã được tự động hủy và hoàn lượt."
            : string.Empty;

        await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
            memberUserId,
            "FREEZE_APPROVED",
            "Đã duyệt tạm ngưng gói tập",
            $"Yêu cầu tạm ngưng {freeze.Days} ngày ({freeze.FromDate:dd/MM/yyyy} – {freeze.ToDate:dd/MM/yyyy}) cho gói {freeze.Membership.Plan.Name} đã được duyệt. Hạn mới: {freeze.Membership.EndDate:dd/MM/yyyy}.{extraMsg}{noteSuffix}",
            "FreezeRequest",
            freeze.Id), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapFreezeDto(freeze, member.User.FullName, member.User.Phone, autoCancelledCount);
    }

    // =========================================================================
    // HÀM HỖ TRỢ NỘI BỘ (HELPERS)
    // =========================================================================

    private async Task PromoteWaitlistsAfterFreezeAsync(
        IEnumerable<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var cutoffHours = GetWaitlistCutoffHours();

        foreach (var sessionId in sessionIds)
        {
            var session = await _classSessionRepository.GetByIdWithDetailsAsync(sessionId, cancellationToken);
            if (session == null ||
                session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                session.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
                continue;

            var startUtc = SchedulingHelpers.Utc(session.StartAt);
            if ((startUtc - nowUtc).TotalHours > cutoffHours)
            {
                var confirmedSeats = await _bookingRepository.CountConfirmedSeatsAsync(sessionId, cancellationToken);
                if (confirmedSeats < session.GymClass.Capacity)
                {
                    var waitingList = await _waitlistEntryRepository.GetWaitingBySessionOrderedAsync(sessionId, cancellationToken);
                    var candidate = waitingList.FirstOrDefault();

                    if (candidate != null)
                    {
                        var candidateMemberships = await _membershipRepository.GetByMemberIdWithPlanAndFreezeAsync(
                            candidate.MemberId,
                            cancellationToken);

                        var sessionVnDate = SchedulingHelpers.ToVietnamDate(startUtc);
                        var (ok, eligibleMs, _) = SchedulingHelpers.EvaluateMembershipForClass(
                            candidateMemberships,
                            session.GymClass.BranchId,
                            session.GymClass.Branch?.Name ?? string.Empty,
                            sessionVnDate);

                        if (ok && eligibleMs != null)
                        {
                            candidate.Status = "PROMOTED";
                            var promotedBooking = new Booking
                            {
                                MemberId = candidate.MemberId,
                                MembershipId = eligibleMs.Id,
                                Type = BookingType.Class,
                                ClassSessionId = session.Id,
                                Status = BookingStatus.Confirmed,
                                CreatedAt = nowUtc
                            };
                            await _bookingRepository.AddAsync(promotedBooking, cancellationToken);

                            var startLocal = startUtc.Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy");
                            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                                candidate.Member.UserId,
                                "WAITLIST_PROMOTED",
                                "Bạn đã được xếp vào lớp!",
                                $"Có chỗ trống ở lớp {session.GymClass.Name} lúc {startLocal}. Bạn đã được tự động chuyển từ danh sách chờ vào lớp chính thức.",
                                "Booking",
                                promotedBooking.Id), cancellationToken);
                        }
                    }
                }
            }

            var updatedSeats = await _bookingRepository.CountConfirmedSeatsAsync(sessionId, cancellationToken);
            session.Status = updatedSeats >= session.GymClass.Capacity ? "FULL" : "SCHEDULED";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ValidatePlanRequest(CreateUpdateMembershipPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Tên gói tập không được để trống.");
        if (request.DurationDays <= 0)
            throw new ValidationException("Thời hạn gói phải lớn hơn 0 ngày.");
        if (request.Price < 0)
            throw new ValidationException("Giá gói tập không được âm.");
    }

    private static string NormalizePlanType(string? planType)
    {
        if (!string.IsNullOrWhiteSpace(planType) && planType.Trim().Equals("PT", StringComparison.OrdinalIgnoreCase))
            return "PT";
        return "MEMBERSHIP";
    }

    private static string NormalizePaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return "ZaloPay";
        var trimmed = method.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "zalopay" => "ZaloPay",
            "vnpay" => "vnpay",
            "card" => "card",
            "banking" => "banking",
            _ => trimmed
        };
    }

    private async Task<List<Guid>> ResolveValidBranchIdsAsync(
        List<Guid>? branchIds,
        CancellationToken cancellationToken)
    {
        if (branchIds == null || branchIds.Count == 0)
            throw new ValidationException("Vui lòng chọn ít nhất một chi nhánh áp dụng khi không bật toàn hệ thống.");

        var existingIds = await _branchRepository.GetExistingIdsAsync(branchIds, cancellationToken);
        if (existingIds.Count == 0)
            throw new ValidationException("Không tìm thấy chi nhánh hợp lệ trong hệ thống.");

        return existingIds.ToList();
    }

    private async Task<Branch?> ResolvePurchaseBranchAsync(
        MembershipPlan plan,
        Guid? requestedBranchId,
        CancellationToken cancellationToken)
    {
        if (requestedBranchId.HasValue && requestedBranchId.Value != Guid.Empty)
        {
            var branch = await _branchRepository.GetByIdAsync(requestedBranchId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chi nhánh đã chọn.");

            if (!plan.IsAllBranches &&
                plan.PlanBranches.Count > 0 &&
                plan.PlanBranches.All(pb => pb.BranchId != branch.Id))
            {
                throw new ValidationException($"Gói '{plan.Name}' không áp dụng cho chi nhánh {branch.Name}.");
            }

            return branch;
        }

        if (!plan.IsAllBranches && plan.PlanBranches.Count > 0)
        {
            var firstAllowedBranchId = plan.PlanBranches.First().BranchId;
            return await _branchRepository.GetByIdAsync(firstAllowedBranchId, cancellationToken);
        }

        return await _branchRepository.GetFirstOrderedByNameAsync(cancellationToken);
    }

    private static MembershipPlanDto MapPlanDto(MembershipPlan p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        DurationDays = p.DurationDays,
        Price = p.Price,
        PriorityLevel = p.PriorityLevel,
        MaxBookingsPerMonth = p.MaxBookingsPerMonth,
        MaxFreezeTimes = p.MaxFreezeTimes,
        MaxFreezeDays = p.MaxFreezeDays,
        IsAllBranches = p.IsAllBranches,
        PlanType = p.PlanType,
        PtSessions = p.PtSessions,
        BranchIds = p.PlanBranches.Select(pb => pb.BranchId).ToList(),
        BranchNames = p.PlanBranches.Where(pb => pb.Branch != null).Select(pb => pb.Branch.Name).ToList(),
        IsActive = true
    };

    private static MembershipDto MapMembershipDto(Membership m, string memberName)
    {
        var approvedFreezes = m.FreezeRequests?
            .Where(f => f.Status == FreezeRequestStatus.Approved)
            .ToList() ?? new List<FreezeRequest>();

        var firstPayment = m.Payments?
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault();

        return new MembershipDto
        {
            Id = m.Id,
            MemberId = m.MemberId,
            MemberName = memberName,
            PlanId = m.PlanId,
            PlanName = m.Plan?.Name ?? string.Empty,
            PlanType = m.Plan?.PlanType ?? "MEMBERSHIP",
            Price = m.Plan?.Price ?? 0,
            PriorityLevel = m.Plan?.PriorityLevel ?? 1,
            DurationDays = m.Plan?.DurationDays ?? 30,
            BranchId = m.BranchId,
            BranchName = m.Branch?.Name,
            IsAllBranches = m.Plan?.IsAllBranches ?? true,
            BranchIds = m.Plan?.PlanBranches?.Select(pb => pb.BranchId).ToList() ?? new List<Guid>(),
            RenewedFromId = m.RenewedFromId,
            IsRenewal = m.RenewedFromId.HasValue,
            StartDate = SchedulingHelpers.Utc(m.StartDate),
            EndDate = SchedulingHelpers.Utc(m.EndDate),
            RemainingSessions = m.RemainingSessions,
            TotalPtSessions = m.Plan?.PtSessions ?? m.RemainingSessions,
            MaxFreezeTimes = m.Plan?.MaxFreezeTimes ?? 0,
            MaxFreezeDays = m.Plan?.MaxFreezeDays ?? 0,
            FreezeCountUsed = approvedFreezes.Count,
            FreezeDaysUsed = approvedFreezes.Sum(f => f.Days),
            Status = m.Status.ToString(),
            PayStatus = m.Status == MembershipStatus.Pending ? "pending_payment" : "paid",
            CreatedAt = firstPayment != null ? SchedulingHelpers.Utc(firstPayment.CreatedAt) : SchedulingHelpers.Utc(m.StartDate)
        };
    }

    private static PaymentDto MapPaymentDto(Payment p, string memberName)
    {
        var plan = p.Membership?.Plan;
        var isPt = plan?.PlanType.Equals("PT", StringComparison.OrdinalIgnoreCase) ?? false;
        var isRenewal = p.Membership?.RenewedFromId.HasValue ?? false;
        var descPrefix = isPt ? "Mua gói PT" : isRenewal ? "Gia hạn gói" : "Mua gói";

        return new PaymentDto
        {
            Id = p.Id,
            MembershipId = p.MembershipId,
            MemberId = p.Membership?.MemberId ?? Guid.Empty,
            MemberName = memberName,
            PlanId = p.Membership?.PlanId ?? Guid.Empty,
            PlanName = plan?.Name ?? string.Empty,
            PlanType = plan?.PlanType ?? "MEMBERSHIP",
            BranchId = p.Membership?.BranchId,
            BranchName = p.Membership?.Branch?.Name,
            Description = plan != null ? $"{descPrefix}: {plan.Name}" : "Thanh toán gói tập",
            Amount = p.Amount,
            Method = p.Method,
            Status = p.Status.ToString(),
            GatewayTxnCode = p.GatewayTxnCode,
            CreatedAt = SchedulingHelpers.Utc(p.CreatedAt),
            PaidAt = SchedulingHelpers.Utc(p.PaidAt)
        };
    }

    private static FreezeRequestDto MapFreezeDto(
        FreezeRequest f,
        string memberName,
        string? memberPhone,
        int affectedBookingsCount)
    {
        var approved = f.Membership?.FreezeRequests?
            .Where(x => x.Status == FreezeRequestStatus.Approved)
            .ToList() ?? new List<FreezeRequest>();

        return new FreezeRequestDto
        {
            Id = f.Id,
            MembershipId = f.MembershipId,
            MemberId = f.Membership?.MemberId ?? Guid.Empty,
            MemberName = memberName,
            MemberPhone = memberPhone,
            PlanId = f.Membership?.PlanId ?? Guid.Empty,
            PlanName = f.Membership?.Plan?.Name ?? string.Empty,
            PlanEndDate = f.Membership != null ? SchedulingHelpers.Utc(f.Membership.EndDate) : DateTime.UtcNow,
            MaxFreezeTimes = f.Membership?.Plan?.MaxFreezeTimes ?? 0,
            MaxFreezeDays = f.Membership?.Plan?.MaxFreezeDays ?? 0,
            FreezeCountUsed = approved.Count,
            FreezeDaysUsed = approved.Sum(x => x.Days),
            AffectedBookingsCount = affectedBookingsCount,
            FromDate = SchedulingHelpers.Utc(f.FromDate),
            ToDate = SchedulingHelpers.Utc(f.ToDate),
            Days = f.Days,
            Reason = f.Reason,
            Status = f.Status.ToString(),
            HandledBy = f.HandledBy,
            HandledByName = f.Handler?.User?.FullName,
            CreatedAt = SchedulingHelpers.Utc(f.CreatedAt),
            HandledAt = null
        };
    }
}
