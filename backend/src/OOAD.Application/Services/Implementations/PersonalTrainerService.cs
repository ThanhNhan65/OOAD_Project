using OOAD.Application.Common.Options;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class PersonalTrainerService : IPersonalTrainerService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ITrainerRepository _trainerRepository;
    private readonly ITrainerAvailabilityRepository _availabilityRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICheckinRepository _checkinRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly BusinessRuleOptions _ruleOptions;
    private readonly IUnitOfWork _unitOfWork;

    public PersonalTrainerService(
        IMemberRepository memberRepository,
        ITrainerRepository trainerRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IMembershipRepository membershipRepository,
        IBookingRepository bookingRepository,
        ICheckinRepository checkinRepository,
        INotificationRepository notificationRepository,
        BusinessRuleOptions ruleOptions,
        IUnitOfWork unitOfWork)
    {
        _memberRepository = memberRepository;
        _trainerRepository = trainerRepository;
        _availabilityRepository = availabilityRepository;
        _membershipRepository = membershipRepository;
        _bookingRepository = bookingRepository;
        _checkinRepository = checkinRepository;
        _notificationRepository = notificationRepository;
        _ruleOptions = ruleOptions;
        _unitOfWork = unitOfWork;
    }

    private int GetCancelDeadlineHours() =>
        _ruleOptions.CancelDeadlineHours > 0 ? _ruleOptions.CancelDeadlineHours : 4;

    public async Task<PtBalanceDto> GetPtBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var now = DateTime.UtcNow;
        var activeMemberships = await _membershipRepository.GetActiveByMemberIdWithPlanAsync(member.Id, now, cancellationToken);

        int freeSessions = 0;
        int packageSessions = 0;
        string? activePlanName = null;
        DateTime? expiryDate = null;

        foreach (var ms in activeMemberships)
        {
            if (activePlanName == null && ms.Plan != null)
            {
                activePlanName = ms.Plan.Name;
                expiryDate = ms.EndDate;
            }

            if (ms.Plan?.PlanType == "PT")
            {
                packageSessions += ms.RemainingSessions;
            }
            else
            {
                freeSessions += ms.RemainingSessions;
            }
        }

        return new PtBalanceDto
        {
            Total = freeSessions + packageSessions,
            FreeSessions = freeSessions,
            PackageSessions = packageSessions,
            ActivePlanName = activePlanName ?? "Chưa có gói hiệu lực",
            ExpiryDate = expiryDate
        };
    }

    public async Task<PtBookingDto> BookPtSessionAsync(Guid userId, BookPtRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var availability = await _availabilityRepository.GetByIdWithTrainerDetailsAsync(request.AvailabilityId, cancellationToken);
        if (availability == null)
            throw new NotFoundException("Khung giờ rảnh không tồn tại.");

        if (availability.IsBooked)
            throw new ValidationException("Khung giờ này đã được học viên khác đặt.");

        if (availability.StartAt <= DateTime.UtcNow)
            throw new ValidationException("Không thể đặt khung giờ đã trôi qua.");

        var sessionVnDate = SchedulingHelpers.ToVietnamDate(availability.StartAt);
        var allMemberships = await _membershipRepository.GetByMemberIdWithPlanAndFreezeAsync(member.Id, cancellationToken);
        var hasValidNonFrozenGym = allMemberships.Any(m =>
            m.Plan != null &&
            m.Plan.PlanType.Equals("MEMBERSHIP", StringComparison.OrdinalIgnoreCase) &&
            m.Status == MembershipStatus.Active &&
            m.StartDate.Date <= sessionVnDate &&
            sessionVnDate <= m.EndDate.Date &&
            !m.FreezeRequests.Any(fr =>
                fr.Status == FreezeRequestStatus.Approved &&
                fr.FromDate.Date <= sessionVnDate &&
                sessionVnDate <= fr.ToDate.Date));

        if (!hasValidNonFrozenGym)
        {
            throw new ValidationException(
                $"Gói hội viên của bạn không còn hiệu lực hoặc đang tạm ngưng (Freeze) vào ngày {sessionVnDate:dd/MM/yyyy}.");
        }

        var frozenOrInvalidIds = allMemberships
            .Where(m =>
                m.Status == MembershipStatus.Frozen ||
                m.EndDate.Date < sessionVnDate ||
                m.FreezeRequests.Any(fr =>
                    fr.Status == FreezeRequestStatus.Approved &&
                    fr.FromDate.Date <= sessionVnDate &&
                    sessionVnDate <= fr.ToDate.Date))
            .Select(m => m.Id)
            .ToHashSet();

        // Tìm gói hội viên còn buổi PT (ưu tiên buổi tặng trước, sau đó tới gói PT)
        var now = DateTime.UtcNow;
        var activeMemberships = await _membershipRepository.GetActiveByMemberIdWithPlanAsync(member.Id, now, cancellationToken);
        var membership = activeMemberships
            .Where(m => m.RemainingSessions > 0 && !frozenOrInvalidIds.Contains(m.Id))
            .OrderBy(m => m.Plan.PlanType == "PT" ? 1 : 0)
            .FirstOrDefault();

        if (membership == null)
            throw new ValidationException("Bạn không còn buổi tập PT nào khả dụng cho ngày hẹn này. Vui lòng mua thêm gói PT hoặc gia hạn gói VIP.");

        // Kiểm tra hội viên có trùng lịch (cả lớp nhóm lẫn lịch PT) vào khung giờ này không
        var startAtUtc = SchedulingHelpers.Utc(availability.StartAt);
        var endAtUtc = SchedulingHelpers.Utc(availability.EndAt);
        var hasConflict = await _bookingRepository.HasTimeConflictAsync(
            member.Id,
            startAtUtc,
            endAtUtc,
            excludeBookingId: null,
            cancellationToken);

        if (hasConflict)
            throw new ValidationException("Bạn đã có một lịch tập (lớp nhóm hoặc PT) khác trùng vào khung giờ này.");

        // Đặt trước 1 buổi
        membership.RemainingSessions -= 1;
        availability.IsBooked = true;

        var booking = new Booking
        {
            MemberId = member.Id,
            MembershipId = membership.Id,
            Type = BookingType.PersonalTrainer,
            AvailabilityId = availability.Id,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _bookingRepository.AddAsync(booking, cancellationToken);

        var startLocalText = startAtUtc.Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy");
        await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
            availability.Trainer.UserId,
            "PT_REQUESTED",
            "Có yêu cầu đặt lịch PT mới",
            $"Hội viên {member.User.FullName} đã gửi yêu cầu tập PT 1-kèm-1 vào lúc {startLocalText}.",
            "Booking",
            booking.Id), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PtBookingDto
        {
            Id = booking.Id,
            MemberId = member.Id,
            MemberName = member.User.FullName,
            MemberPhone = member.User.Phone,
            TrainerId = availability.Trainer.Id,
            TrainerName = availability.Trainer.User.FullName,
            TrainerSpecialty = availability.Trainer.Specialty,
            BranchId = availability.Trainer.BranchId,
            BranchName = availability.Trainer.Branch?.Name ?? "Chi nhánh OOAD",
            AvailabilityId = availability.Id,
            StartAt = availability.StartAt,
            EndAt = availability.EndAt,
            Status = booking.Status.ToString().ToUpper(),
            Note = request.Note,
            SourceType = membership.Plan?.PlanType == "PT" ? "pt_package" : "membership_free",
            CheckedIn = false,
            CanCancel = true,
            CanReview = false,
            IsReviewed = false,
            CreatedAt = booking.CreatedAt
        };
    }

    public async Task<IEnumerable<PtBookingDto>> GetMyPtBookingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            return Enumerable.Empty<PtBookingDto>();

        var bookings = await _bookingRepository.GetPtBookingsByMemberIdAsync(member.Id, cancellationToken);
        var now = DateTime.UtcNow;

        return bookings.Select(b =>
        {
            var av = b.Availability!;
            var isAttended = b.Status == BookingStatus.Attended;
            var hasCheckedIn = b.Checkin != null || isAttended;
            return new PtBookingDto
            {
                Id = b.Id,
                MemberId = member.Id,
                MemberName = b.Member?.User?.FullName ?? "Hội viên",
                MemberPhone = b.Member?.User?.Phone,
                TrainerId = av.TrainerId,
                TrainerName = av.Trainer?.User?.FullName ?? "HLV",
                TrainerSpecialty = av.Trainer?.Specialty,
                BranchId = av.Trainer?.BranchId ?? Guid.Empty,
                BranchName = av.Trainer?.Branch?.Name ?? "Chi nhánh OOAD",
                AvailabilityId = av.Id,
                StartAt = av.StartAt,
                EndAt = av.EndAt,
                Status = b.Status.ToString().ToUpper(),
                SourceType = b.Membership?.Plan?.PlanType == "PT" ? "pt_package" : "membership_free",
                CheckedIn = hasCheckedIn,
                CanCancel = b.Status == BookingStatus.Pending || (b.Status == BookingStatus.Confirmed && av.StartAt > now),
                CanReview = isAttended && b.Review == null,
                IsReviewed = b.Review != null,
                CreatedAt = b.CreatedAt
            };
        });
    }

    public async Task CancelPtBookingAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetByUserIdWithUserAsync(userId, asNoTracking: true, cancellationToken);
        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var booking = await _bookingRepository.GetByIdAndMemberIdWithPtDetailsAsync(bookingId, member.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy thông tin đặt lịch PT.");

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
            throw new ValidationException("Không thể hủy buổi tập này.");

        var now = DateTime.UtcNow;
        var cancelDeadlineHours = GetCancelDeadlineHours();
        var isTimely = true;

        if (booking.Availability != null)
        {
            if (booking.Status == BookingStatus.Confirmed)
            {
                if (booking.Availability.StartAt <= now)
                    throw new ValidationException("Buổi tập đã bắt đầu hoặc đã kết thúc, không thể hủy.");

                var hoursLeft = (booking.Availability.StartAt - now).TotalHours;
                isTimely = hoursLeft >= cancelDeadlineHours;
            }

            booking.Availability.IsBooked = false;
        }

        booking.Status = isTimely ? BookingStatus.Cancelled : BookingStatus.LateCancelled;
        booking.CancelledAt = now;

        // Chỉ hoàn buổi nếu đang Pending hoặc hủy đúng hạn (>= CancelDeadlineHours)
        if (isTimely && booking.Membership != null)
        {
            booking.Membership.RemainingSessions += 1;
        }

        var startLocalText = booking.Availability != null
            ? SchedulingHelpers.Utc(booking.Availability.StartAt).Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy")
            : string.Empty;

        await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
            member.UserId,
            "PT_BOOKING_CANCELLED",
            "Đã hủy lịch hẹn PT",
            isTimely
                ? $"Bạn đã hủy lịch PT lúc {startLocalText} đúng hạn và được hoàn lại 1 buổi tập."
                : $"Bạn đã hủy lịch PT lúc {startLocalText} sát giờ (dưới {cancelDeadlineHours}h), buổi tập không được hoàn lại.",
            "Booking",
            booking.Id), cancellationToken);

        if (booking.Availability?.Trainer != null)
        {
            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                booking.Availability.Trainer.UserId,
                "PT_BOOKING_CANCELLED",
                "Hội viên đã hủy lịch hẹn PT",
                $"Hội viên {member.User.FullName} đã hủy lịch hẹn PT lúc {startLocalText}.{(string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason.Trim()}")}",
                "Booking",
                booking.Id), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<TrainerPtRequestItemDto>> GetTrainerPtRequestsAsync(Guid trainerUserId, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdWithDetailsAsync(
            trainerUserId,
            includeAvailabilities: false,
            asNoTracking: true,
            cancellationToken);

        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var bookings = (await _bookingRepository.GetPtBookingsByTrainerIdAsync(trainer.Id, cancellationToken: cancellationToken)).ToList();

        // Đối soát thêm check-in tại chi nhánh trong ngày diễn ra buổi tập cho các lịch Confirmed
        var confirmedDates = bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.Checkin == null && b.Availability != null)
            .GroupBy(b => SchedulingHelpers.ToVietnamDate(b.Availability!.StartAt))
            .ToList();

        var checkedInByDate = new Dictionary<DateTime, HashSet<Guid>>();
        foreach (var group in confirmedDates)
        {
            var dayFromUtc = group.Key.Subtract(SchedulingHelpers.VietnamOffset);
            var dayToUtc = group.Key.AddDays(1).Subtract(SchedulingHelpers.VietnamOffset);
            var times = await _checkinRepository.GetCheckedInMemberTimesOnDateAsync(
                trainer.BranchId,
                dayFromUtc,
                dayToUtc,
                group.Select(b => b.MemberId),
                cancellationToken);
            checkedInByDate[group.Key] = times.Keys.ToHashSet();
        }

        return bookings.Select(b =>
        {
            var av = b.Availability!;
            var vnDate = SchedulingHelpers.ToVietnamDate(av.StartAt);
            var hasCheckedIn = b.Checkin != null
                || b.Status == BookingStatus.Attended
                || (checkedInByDate.TryGetValue(vnDate, out var set) && set.Contains(b.MemberId));

            return new TrainerPtRequestItemDto
            {
                Id = b.Id,
                MemberId = b.MemberId,
                MemberName = b.Member?.User?.FullName ?? "Hội viên",
                MemberPhone = b.Member?.User?.Phone,
                BranchId = trainer.BranchId,
                BranchName = trainer.Branch?.Name ?? "Chi nhánh OOAD",
                StartAt = av.StartAt,
                EndAt = av.EndAt,
                Status = b.Status.ToString().ToUpper(),
                SourceType = b.Membership?.Plan?.PlanType == "PT" ? "pt_package" : "membership_free",
                CheckedIn = hasCheckedIn,
                CreatedAt = b.CreatedAt
            };
        });
    }

    public async Task RespondPtRequestAsync(Guid trainerUserId, Guid bookingId, bool accept, string? reason, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var booking = await _bookingRepository.GetByIdAndTrainerIdWithPtDetailsAsync(bookingId, trainer.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy yêu cầu đặt lịch PT.");

        if (booking.Status != BookingStatus.Pending)
            throw new ValidationException("Yêu cầu này không ở trạng thái chờ duyệt.");

        var startLocalText = booking.Availability != null
            ? SchedulingHelpers.Utc(booking.Availability.StartAt).Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy")
            : string.Empty;

        if (accept)
        {
            booking.Status = BookingStatus.Confirmed;
            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                booking.Member.UserId,
                "PT_CONFIRMED",
                "Lịch hẹn PT đã được xác nhận",
                $"HLV {booking.Availability?.Trainer?.User?.FullName ?? "PT"} đã xác nhận lịch tập 1-kèm-1 vào lúc {startLocalText}.",
                "Booking",
                booking.Id), cancellationToken);
        }
        else
        {
            booking.Status = BookingStatus.Rejected;
            booking.CancelledAt = DateTime.UtcNow;
            if (booking.Availability != null)
            {
                booking.Availability.IsBooked = false;
            }
            if (booking.Membership != null)
            {
                booking.Membership.RemainingSessions += 1;
            }

            var reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Lý do: {reason.Trim()}";
            await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
                booking.Member.UserId,
                "PT_REJECTED",
                "Yêu cầu đặt lịch PT bị từ chối",
                $"HLV đã từ chối yêu cầu lịch tập PT lúc {startLocalText} và hoàn lại 1 buổi tập cho bạn.{reasonText}",
                "Booking",
                booking.Id), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CompletePtSessionAsync(Guid trainerUserId, Guid bookingId, bool attended, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var booking = await _bookingRepository.GetByIdAndTrainerIdWithPtDetailsAsync(bookingId, trainer.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy buổi tập PT.");

        if (booking.Status != BookingStatus.Confirmed)
            throw new ValidationException("Chỉ có thể xác nhận hoàn thành hoặc vắng mặt cho buổi tập PT đang ở trạng thái Đã xác nhận.");

        if (attended)
        {
            var hasValidCheckin = booking.Checkin != null;
            if (!hasValidCheckin && booking.Availability != null)
            {
                var sessionVnDate = SchedulingHelpers.ToVietnamDate(booking.Availability.StartAt);
                var dayFromUtc = sessionVnDate.Subtract(SchedulingHelpers.VietnamOffset);
                var dayToUtc = sessionVnDate.AddDays(1).Subtract(SchedulingHelpers.VietnamOffset);
                var checkedInTimes = await _checkinRepository.GetCheckedInMemberTimesOnDateAsync(
                    trainer.BranchId,
                    dayFromUtc,
                    dayToUtc,
                    new[] { booking.MemberId },
                    cancellationToken);
                hasValidCheckin = checkedInTimes.ContainsKey(booking.MemberId);
            }

            if (!hasValidCheckin)
            {
                throw new ValidationException("Hội viên chưa check-in tại chi nhánh trong ngày diễn ra buổi tập nên chưa thể xác nhận hoàn thành.");
            }
        }

        booking.Status = attended ? BookingStatus.Attended : BookingStatus.NoShow;

        var startLocalText = booking.Availability != null
            ? SchedulingHelpers.Utc(booking.Availability.StartAt).Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy")
            : string.Empty;

        await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
            booking.Member.UserId,
            attended ? "PT_COMPLETED" : "PT_NOSHOW",
            attended ? "Hoàn thành buổi tập PT" : "Ghi nhận vắng mặt buổi PT",
            attended
                ? $"Buổi tập PT lúc {startLocalText} đã được xác nhận hoàn thành. Hãy gửi đánh giá cho HLV nhé!"
                : $"Bạn được ghi nhận vắng mặt (No-show) ở buổi tập PT lúc {startLocalText}.",
            "Booking",
            booking.Id), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelPtByTrainerAsync(Guid trainerUserId, Guid bookingId, string reason, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(trainerUserId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Không tìm thấy hồ sơ HLV.");

        var booking = await _bookingRepository.GetByIdAndTrainerIdWithPtDetailsAsync(bookingId, trainer.Id, cancellationToken);
        if (booking == null)
            throw new NotFoundException("Không tìm thấy buổi tập PT.");

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
            throw new ValidationException("Chỉ có thể hủy lịch PT đang ở trạng thái Chờ duyệt hoặc Đã xác nhận.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        if (booking.Availability != null)
        {
            booking.Availability.IsBooked = false;
        }

        if (booking.Membership != null)
        {
            booking.Membership.RemainingSessions += 1;
        }

        var startLocalText = booking.Availability != null
            ? SchedulingHelpers.Utc(booking.Availability.StartAt).Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy")
            : string.Empty;
        var reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Lý do: {reason.Trim()}";

        await _notificationRepository.AddAsync(SchedulingHelpers.BuildNotification(
            booking.Member.UserId,
            "PT_CANCELLED_BY_TRAINER",
            "HLV đã hủy lịch hẹn PT",
            $"HLV đã hủy buổi tập PT lúc {startLocalText} và hoàn lại 1 buổi tập cho bạn.{reasonText}",
            "Booking",
            booking.Id), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
