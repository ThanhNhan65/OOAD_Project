using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

/// <summary>
/// Service điều phối toàn bộ nghiệp vụ Access Control (Check-in / Check-out,
/// kiểm tra điều kiện gói tập & Freeze, quyền tập chéo chi nhánh, tự động điểm danh lớp/PT
/// và tự động check-out cuối ngày).
/// </summary>
public class CheckinService : ICheckinService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly ICheckinRepository _checkinRepository;
    private readonly IMemberCardRepository _memberCardRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly IFreezeRequestRepository _freezeRequestRepository;
    private readonly IEquipmentIssueRepository _equipmentIssueRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CheckinService(
        ICheckinRepository checkinRepository,
        IMemberCardRepository memberCardRepository,
        IMemberRepository memberRepository,
        IMembershipRepository membershipRepository,
        IBookingRepository bookingRepository,
        IBranchRepository branchRepository,
        IStaffRepository staffRepository,
        IFreezeRequestRepository freezeRequestRepository,
        IEquipmentIssueRepository equipmentIssueRepository,
        IUnitOfWork unitOfWork)
    {
        _checkinRepository = checkinRepository;
        _memberCardRepository = memberCardRepository;
        _memberRepository = memberRepository;
        _membershipRepository = membershipRepository;
        _bookingRepository = bookingRepository;
        _branchRepository = branchRepository;
        _staffRepository = staffRepository;
        _freezeRequestRepository = freezeRequestRepository;
        _equipmentIssueRepository = equipmentIssueRepository;
        _unitOfWork = unitOfWork;
    }

    // 1. Check-in bằng thẻ / mã QR (Kiosk & Quầy lễ tân)
    public async Task<CheckinOperationResultDto> CheckInByCardAsync(
        CardCheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode))
            throw new ValidationException("Mã thẻ / QR không được để trống.");

        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy chi nhánh với mã: {request.BranchId}");

        if (!branch.IsActive || !branch.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "BRANCH_INACTIVE",
                Message = $"Chi nhánh {branch.Name} hiện đang tạm ngưng hoạt động."
            };
        }

        var card = await _memberCardRepository.GetByCardCodeAsync(request.CardCode, cancellationToken);
        if (card == null)
        {
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "CARD_INVALID",
                CardCode = request.CardCode.Trim().ToUpperInvariant(),
                Message = "Không nhận diện được mã thẻ / QR trên hệ thống."
            };
        }

        if (!card.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            var latestMs = card.Member.Memberships.OrderByDescending(m => m.EndDate).FirstOrDefault();
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "CARD_LOCKED",
                MemberId = card.Member.Id,
                MemberName = card.Member.User.FullName,
                CardCode = card.CardCode,
                CardStatus = card.Status.ToUpperInvariant(),
                PlanName = latestMs?.Plan?.Name,
                MembershipStatus = latestMs?.Status.ToString().ToUpperInvariant(),
                PlanEndDate = latestMs?.EndDate,
                Message = $"Thẻ {card.CardCode} đang ở trạng thái {card.Status.ToUpperInvariant()} nên không thể quét cổng. Vui lòng liên hệ lễ tân để check-in thủ công hoặc cấp thẻ mới."
            };
        }

        var method = request.Method.Equals("Card", StringComparison.OrdinalIgnoreCase)
            ? CheckinMethod.Card
            : CheckinMethod.QR;

        return await ValidateAndCreateCheckinAsync(
            card.Member,
            branch,
            card,
            method,
            recordedByStaffId: null,
            note: null,
            cancellationToken);
    }

    // 2. Check-in thủ công tại quầy lễ tân (UC-20)
    public async Task<CheckinOperationResultDto> CheckInManualAsync(
        ManualCheckinRequest request,
        Guid staffUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Vui lòng nhập lý do check-in thủ công.");

        var staff = await _staffRepository.GetByUserIdWithBranchAsync(staffUserId, cancellationToken);
        var targetBranchId = request.BranchId ?? staff?.BranchId ?? Guid.Empty;
        if (targetBranchId == Guid.Empty)
            throw new ValidationException("Không xác định được chi nhánh check-in.");

        var branch = await _branchRepository.GetByIdAsync(targetBranchId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy chi nhánh với mã: {targetBranchId}");

        var member = await _memberRepository.GetByIdOrUserIdWithDetailsAsync(request.MemberId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy hội viên với mã: {request.MemberId}");

        return await ValidateAndCreateCheckinAsync(
            member,
            branch,
            card: null, // Quy định UC-20: Check-in thủ công ghi Card_Id = NULL
            CheckinMethod.Manual,
            recordedByStaffId: staff?.Id,
            note: request.Reason.Trim(),
            cancellationToken);
    }

    // 3. Check-out bằng thẻ / QR
    public async Task<CheckinOperationResultDto> CheckOutByCardAsync(
        CardCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode))
            throw new ValidationException("Mã thẻ / QR không được để trống.");

        var card = await _memberCardRepository.GetByCardCodeAsync(request.CardCode, cancellationToken);
        if (card == null)
        {
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "CARD_INVALID",
                Message = "Không nhận diện được mã thẻ / QR."
            };
        }

        var openCheckin = await _checkinRepository.GetOpenCheckinByMemberIdAsync(card.MemberId, cancellationToken);
        if (openCheckin == null)
        {
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "NO_OPEN_CHECKIN",
                MemberId = card.MemberId,
                MemberName = card.Member.User.FullName,
                CardCode = card.CardCode,
                CardStatus = card.Status.ToUpperInvariant(),
                Message = "Hội viên chưa check-in hoặc đã check-out trước đó."
            };
        }

        return await CompleteCheckoutAsync(openCheckin, cancellationToken);
    }

    // 4. Check-out theo ID lượt check-in
    public async Task<CheckinOperationResultDto> CheckOutByIdAsync(
        Guid checkinId,
        CancellationToken cancellationToken = default)
    {
        var checkin = await _checkinRepository.GetByIdWithDetailsAsync(checkinId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy lượt check-in với mã: {checkinId}");

        if (checkin.CheckOutAt.HasValue)
        {
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "ALREADY_CHECKED_OUT",
                MemberId = checkin.MemberId,
                MemberName = checkin.Member.User.FullName,
                Checkin = MapToDto(checkin),
                Message = "Lượt check-in này đã được check-out trước đó."
            };
        }

        return await CompleteCheckoutAsync(checkin, cancellationToken);
    }

    // 5. Check-out thủ công theo MemberId / UserId
    public async Task<CheckinOperationResultDto> CheckOutManualAsync(
        ManualCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByIdOrUserIdWithDetailsAsync(request.MemberId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy hội viên với mã: {request.MemberId}");

        var openCheckin = await _checkinRepository.GetOpenCheckinByMemberIdAsync(member.Id, cancellationToken);
        if (openCheckin == null)
        {
            return new CheckinOperationResultDto
            {
                Allowed = false,
                Action = "DENIED",
                DenyCode = "NO_OPEN_CHECKIN",
                MemberId = member.Id,
                MemberName = member.User.FullName,
                Message = "Hội viên hiện không có lượt check-in nào đang mở."
            };
        }

        return await CompleteCheckoutAsync(openCheckin, cancellationToken);
    }

    // 6. Danh sách người đang tập tại chi nhánh (CheckOutAt == null)
    public async Task<IReadOnlyList<CheckinDto>> GetActiveCheckinsAsync(
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var active = await _checkinRepository.GetActiveCheckinsAsync(branchId, cancellationToken);
        return active.Select(MapToDto).ToList();
    }

    // 7. Lịch sử check-in có lọc (Staff / Admin)
    public async Task<IReadOnlyList<CheckinDto>> GetHistoryAsync(
        Guid? branchId,
        string? method,
        DateTime? date,
        string? search,
        CancellationToken cancellationToken = default)
    {
        CheckinMethod? parsedMethod = null;
        if (!string.IsNullOrWhiteSpace(method) && !method.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<CheckinMethod>(method, ignoreCase: true, out var m))
                parsedMethod = m;
        }

        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        if (date.HasValue)
        {
            // Chuyển ngày chọn theo giờ Việt Nam (UTC+7) về khoảng UTC để truy vấn chính xác
            var startLocal = date.Value.Date;
            fromUtc = startLocal.Subtract(VietnamOffset);
            toUtc = startLocal.AddDays(1).Subtract(VietnamOffset);
        }

        var list = await _checkinRepository.GetHistoryAsync(
            branchId,
            memberId: null,
            parsedMethod,
            fromUtc,
            toUtc,
            search,
            take: 200,
            cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    // 8. Lịch sử check-in của hội viên đang đăng nhập (/member/card)
    public async Task<IReadOnlyList<CheckinDto>> GetMyHistoryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdAsync(currentUserId, cancellationToken);
        if (member == null) return [];

        var list = await _checkinRepository.GetHistoryAsync(
            branchId: null,
            memberId: member.Id,
            method: null,
            fromDateUtc: null,
            toDateUtc: null,
            search: null,
            take: 50,
            cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    // 9. Dashboard Trang chủ lễ tân (/staff)
    public async Task<StaffDashboardDto> GetStaffDashboardAsync(
        Guid staffUserId,
        Guid? overrideBranchId = null,
        CancellationToken cancellationToken = default)
    {
        var staff = await _staffRepository.GetByUserIdWithBranchAsync(staffUserId, cancellationToken);
        Branch? branch = null;

        if (overrideBranchId.HasValue && overrideBranchId.Value != Guid.Empty)
            branch = await _branchRepository.GetByIdAsync(overrideBranchId.Value, cancellationToken);
        else if (staff != null)
            branch = staff.Branch ?? await _branchRepository.GetByIdAsync(staff.BranchId, cancellationToken);
        else
            branch = (await _branchRepository.GetAllAsync(cancellationToken)).FirstOrDefault();

        if (branch == null)
            throw new NotFoundException("Không tìm thấy chi nhánh làm việc cho lễ tân.");

        var active = await _checkinRepository.GetActiveCheckinsAsync(branch.Id, cancellationToken);

        var todayLocal = DateTime.UtcNow.Add(VietnamOffset).Date;
        var fromUtc = todayLocal.Subtract(VietnamOffset);
        var toUtc = todayLocal.AddDays(1).Subtract(VietnamOffset);

        var todayHistory = await _checkinRepository.GetHistoryAsync(
            branch.Id,
            memberId: null,
            method: null,
            fromUtc,
            toUtc,
            search: null,
            take: 500,
            cancellationToken);

        var pendingFreezeCount = await _freezeRequestRepository.CountPendingAsync(cancellationToken);

        var openIssueCount = await _equipmentIssueRepository.CountOpenByBranchAsync(branch.Id, cancellationToken);

        return new StaffDashboardDto
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            OperatingHours = branch.OperatingHours,
            PresentCount = active.Count,
            TodayCheckinCount = todayHistory.Count,
            TodayManualCheckinCount = todayHistory.Count(c => c.Method == CheckinMethod.Manual),
            PendingFreezeCount = pendingFreezeCount,
            OpenEquipmentIssueCount = openIssueCount,
            PresentMembers = active.Select(MapToDto).ToList()
        };
    }

    // 10. Tự động Check-out các lượt chưa đóng khi quá giờ đóng cửa chi nhánh (22h00)
    public async Task<int> AutoCheckoutExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var openCheckins = await _checkinRepository.GetAllOpenCheckinsWithBranchAsync(cancellationToken);
        if (openCheckins.Count == 0) return 0;

        var nowUtc = DateTime.UtcNow;
        var nowLocal = nowUtc.Add(VietnamOffset);
        var closedCount = 0;

        foreach (var checkin in openCheckins)
        {
            var checkInLocal = checkin.CheckInAt.Add(VietnamOffset);
            var closeTime = checkin.Branch?.CloseTime ?? new TimeSpan(22, 0, 0);

            var isFromPreviousDay = checkInLocal.Date < nowLocal.Date;
            var isPastCloseTimeToday = checkInLocal.Date == nowLocal.Date && nowLocal.TimeOfDay >= closeTime;

            if (isFromPreviousDay || isPastCloseTimeToday)
            {
                checkin.CheckOutAt = nowUtc;
                checkin.IsAutoCheckout = true;
                checkin.Note = string.IsNullOrWhiteSpace(checkin.Note)
                    ? "Tự động check-out lúc đóng cửa chi nhánh"
                    : $"{checkin.Note} | Tự động check-out lúc đóng cửa";
                _checkinRepository.Update(checkin);
                closedCount++;
            }
        }

        if (closedCount > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return closedCount;
    }

    // ================= HÀM LÕI DÙNG CHUNG (DRY & SRP) =================

    private async Task<CheckinOperationResultDto> ValidateAndCreateCheckinAsync(
        Member member,
        Branch branch,
        MemberCard? card,
        CheckinMethod method,
        Guid? recordedByStaffId,
        string? note,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var todayVn = nowUtc.Add(VietnamOffset).Date;
        var primaryCard = card ?? member.MemberCards.FirstOrDefault(c => c.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            ?? member.MemberCards.FirstOrDefault();

        // Bước A: Kiểm tra trạng thái tài khoản User
        if (member.User.Status == UserStatus.Locked)
        {
            return BuildDenied(member, primaryCard, null, "USER_LOCKED",
                "Tài khoản hội viên đang bị khóa (LOCKED). Không thể check-in.");
        }

        // Bước B: Kiểm tra hội viên đã có lượt check-in mở hay chưa
        var existingOpen = await _checkinRepository.GetOpenCheckinByMemberIdAsync(member.Id, cancellationToken);
        if (existingOpen != null)
        {
            return BuildDenied(member, primaryCard, null, "ALREADY_CHECKED_IN",
                $"Hội viên đang có mặt tại {existingOpen.Branch.Name} (vào lúc {existingOpen.CheckInAt.Add(VietnamOffset):HH:mm}). Vui lòng check-out trước.");
        }

        // Bước C: Kiểm tra gói hội viên (UC-21: Còn hạn, Không Freeze) & Quyền tập chéo (UC-24)
        var allMemberships = await _membershipRepository.GetByMemberIdWithPlanAndFreezeAsync(member.Id, cancellationToken);
        var gymMemberships = allMemberships
            .Where(m => m.Plan.PlanType.Equals("MEMBERSHIP", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (gymMemberships.Count == 0)
        {
            return BuildDenied(member, primaryCard, null, "NO_MEMBERSHIP",
                "Hội viên chưa đăng ký gói tập nào. Vui lòng đăng ký gói tại quầy hoặc trên ứng dụng.");
        }

        var currentMembership = gymMemberships.FirstOrDefault(m =>
                (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Frozen) &&
                m.StartDate.Date <= todayVn && m.EndDate.Date >= todayVn)
            ?? gymMemberships.First();

        // Kiểm tra Freeze (theo trạng thái gói HOẶC có đơn FreezeRequest đã duyệt trùng hôm nay)
        var isFrozenToday = currentMembership.Status == MembershipStatus.Frozen
            || currentMembership.FreezeRequests.Any(fr =>
                fr.Status == FreezeRequestStatus.Approved &&
                fr.FromDate.Date <= todayVn &&
                fr.ToDate.Date >= todayVn);

        if (isFrozenToday)
        {
            return BuildDenied(member, primaryCard, currentMembership, "FROZEN",
                $"Gói tập '{currentMembership.Plan.Name}' đang trong thời gian bảo lưu (Freeze). Không thể check-in.");
        }

        // Kiểm tra hạn sử dụng
        if (currentMembership.Status != MembershipStatus.Active
            || currentMembership.EndDate.Date < todayVn
            || currentMembership.StartDate.Date > todayVn)
        {
            return BuildDenied(member, primaryCard, currentMembership, "EXPIRED",
                $"Gói tập '{currentMembership.Plan.Name}' đã hết hạn hoặc chưa đến ngày kích hoạt (HSD: {currentMembership.EndDate:dd/MM/yyyy}).");
        }

        // Kiểm tra quyền tập chéo chi nhánh (UC-24)
        var allowedBranch = currentMembership.Plan.IsAllBranches
            || currentMembership.BranchId == branch.Id
            || currentMembership.Plan.PlanBranches.Any(pb => pb.BranchId == branch.Id);

        if (!allowedBranch)
        {
            return BuildDenied(member, primaryCard, currentMembership, "WRONG_BRANCH",
                $"Gói tập '{currentMembership.Plan.Name}' không hỗ trợ tập chéo tại {branch.Name}.");
        }

        // Bước D: Ghi nhận liên kết Check-in với lịch Lớp nhóm / PT 1-kèm-1 trong khung giờ (chờ HLV điểm danh -> Attended)
        var eligibleBooking = await _bookingRepository.FindEligibleBookingForCheckinAsync(
            member.Id, branch.Id, nowUtc, cancellationToken);

        string? attendedSummary = null;
        if (eligibleBooking != null)
        {
            attendedSummary = eligibleBooking.Type == BookingType.Class && eligibleBooking.ClassSession != null
                ? $"Lớp {eligibleBooking.ClassSession.GymClass.Name} ({eligibleBooking.ClassSession.StartAt.Add(VietnamOffset):HH:mm})"
                : "Buổi tập PT 1-kèm-1";
        }

        // Bước E: Tạo bản ghi Checkin & Commit 1 transaction (ACID) qua UnitOfWork
        var checkin = new Checkin
        {
            MemberId = member.Id,
            BranchId = branch.Id,
            CardId = card?.Id,
            BookingId = eligibleBooking?.Id,
            RecordedBy = recordedByStaffId,
            Method = method,
            Note = note,
            CheckInAt = nowUtc,
            IsAutoCheckout = false
        };

        await _checkinRepository.AddAsync(checkin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _checkinRepository.GetByIdWithDetailsAsync(checkin.Id, cancellationToken) ?? checkin;
        var successMsg = attendedSummary != null
            ? $"Check-in thành công! Đã ghi nhận vào cổng cho: {attendedSummary} (chờ HLV xác nhận điểm danh)."
            : $"Check-in thành công tại {branch.Name}. Chúc bạn tập luyện hiệu quả!";

        return new CheckinOperationResultDto
        {
            Allowed = true,
            Action = "CHECK_IN",
            Message = successMsg,
            Checkin = MapToDto(saved),
            MemberId = member.Id,
            MemberName = member.User.FullName,
            CardCode = primaryCard?.CardCode,
            CardStatus = primaryCard?.Status.ToUpperInvariant(),
            PlanName = currentMembership.Plan.Name,
            MembershipStatus = currentMembership.Status.ToString().ToUpperInvariant(),
            PlanEndDate = currentMembership.EndDate,
            AutoAttendedBooking = eligibleBooking != null,
            AttendedBookingSummary = attendedSummary
        };
    }

    private async Task<CheckinOperationResultDto> CompleteCheckoutAsync(
        Checkin openCheckin,
        CancellationToken cancellationToken)
    {
        openCheckin.CheckOutAt = DateTime.UtcNow;
        openCheckin.IsAutoCheckout = false;
        _checkinRepository.Update(openCheckin);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mins = (int)Math.Max(1, Math.Round((openCheckin.CheckOutAt.Value - openCheckin.CheckInAt).TotalMinutes));
        var durationText = mins >= 60 ? $"{mins / 60} giờ {mins % 60} phút" : $"{mins} phút";

        return new CheckinOperationResultDto
        {
            Allowed = true,
            Action = "CHECK_OUT",
            Message = $"Check-out thành công! Thời lượng tập: {durationText}. Hẹn gặp lại!",
            Checkin = MapToDto(openCheckin),
            MemberId = openCheckin.MemberId,
            MemberName = openCheckin.Member?.User?.FullName,
            CardCode = openCheckin.Card?.CardCode
        };
    }

    private static CheckinOperationResultDto BuildDenied(
        Member member,
        MemberCard? card,
        Membership? membership,
        string denyCode,
        string message) => new()
    {
        Allowed = false,
        Action = "DENIED",
        DenyCode = denyCode,
        Message = message,
        MemberId = member.Id,
        MemberName = member.User.FullName,
        CardCode = card?.CardCode,
        CardStatus = card?.Status.ToUpperInvariant(),
        PlanName = membership?.Plan?.Name,
        MembershipStatus = membership?.Status.ToString().ToUpperInvariant(),
        PlanEndDate = membership?.EndDate
    };

    private static CheckinDto MapToDto(Checkin c)
    {
        int? durationMins = c.CheckOutAt.HasValue
            ? (int)Math.Max(1, Math.Round((c.CheckOutAt.Value - c.CheckInAt).TotalMinutes))
            : null;

        var activeCardCode = c.Card?.CardCode
            ?? c.Member?.MemberCards?.FirstOrDefault(mc => mc.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))?.CardCode
            ?? c.Member?.MemberCards?.FirstOrDefault()?.CardCode;

        return new CheckinDto
        {
            Id = c.Id,
            MemberId = c.MemberId,
            UserId = c.Member?.UserId ?? Guid.Empty,
            MemberName = c.Member?.User?.FullName ?? string.Empty,
            MemberEmail = c.Member?.User?.Email ?? string.Empty,
            MemberPhone = c.Member?.User?.Phone,
            CardCode = activeCardCode,
            BranchId = c.BranchId,
            BranchName = c.Branch?.Name ?? string.Empty,
            CardId = c.CardId,
            BookingId = c.BookingId,
            AttendedClassName = c.Booking?.ClassSession?.GymClass?.Name,
            RecordedBy = c.RecordedBy,
            RecordedByName = c.Recorder?.User?.FullName,
            Method = c.Method.ToString(),
            Note = c.Note,
            CheckInAt = c.CheckInAt,
            CheckOutAt = c.CheckOutAt,
            IsAutoCheckout = c.IsAutoCheckout,
            DurationMinutes = durationMins
        };
    }
}