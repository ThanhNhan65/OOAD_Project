using System.Data;
using OOAD.Application.Common.Options;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class ClassBookingService : IClassBookingService
{
    private readonly IClassSessionRepository _classSessionRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly BusinessRuleOptions _ruleOptions;
    private readonly IUnitOfWork _unitOfWork;

    public ClassBookingService(
        IClassSessionRepository classSessionRepository,
        IWaitlistEntryRepository waitlistEntryRepository,
        IBookingRepository bookingRepository,
        IMemberRepository memberRepository,
        IMembershipRepository membershipRepository,
        IReviewRepository reviewRepository,
        INotificationRepository notificationRepository,
        BusinessRuleOptions ruleOptions,
        IUnitOfWork unitOfWork)
    {
        _classSessionRepository = classSessionRepository;
        _waitlistEntryRepository = waitlistEntryRepository;
        _bookingRepository = bookingRepository;
        _memberRepository = memberRepository;
        _membershipRepository = membershipRepository;
        _reviewRepository = reviewRepository;
        _notificationRepository = notificationRepository;
        _ruleOptions = ruleOptions;
        _unitOfWork = unitOfWork;
    }

    private int GetWaitlistCutoffHours() =>
        _ruleOptions.WaitlistCutoffHours > 0 ? _ruleOptions.WaitlistCutoffHours : 8;

    private int GetCancelDeadlineHours() =>
        _ruleOptions.CancelDeadlineHours > 0 ? _ruleOptions.CancelDeadlineHours : 4;

    public async Task<RegisterClassResponseDto> RegisterClassSessionAsync(
        Guid classSessionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var waitlistCutoffHours = GetWaitlistCutoffHours();
        var cancelDeadlineHours = GetCancelDeadlineHours();

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var nowUtc = DateTime.UtcNow;
            var member = await _memberRepository.GetByUserIdAsync(currentUserId, ct)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ hội viên cho tài khoản hiện tại.");

            if (member.User.Status == UserStatus.Locked)
                throw new ValidationException("Tài khoản của bạn đang bị khóa. Không thể đặt lịch lớp học.");

            var session = await _classSessionRepository.GetByIdWithDetailsAsync(classSessionId, ct)
                ?? throw new NotFoundException($"Không tìm thấy buổi học với mã: {classSessionId}");

            if (session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                session.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("Buổi học này đã bị hủy hoặc đã kết thúc.");
            }

            var startAtUtc = SchedulingHelpers.Utc(session.StartAt);
            var endAtUtc = SchedulingHelpers.Utc(session.EndAt);
            if (startAtUtc <= nowUtc)
                throw new ValidationException("Buổi học đã bắt đầu hoặc đã qua, không thể đăng ký.");

            var sessionVnDateTime = startAtUtc.Add(SchedulingHelpers.VietnamOffset);
            var sessionVnDate = sessionVnDateTime.Date;

            var memberships = await _membershipRepository.GetByMemberIdWithPlanAndFreezeAsync(member.Id, ct);
            var (ok, eligibleMembership, errorReason) = SchedulingHelpers.EvaluateMembershipForClass(
                memberships,
                session.GymClass.BranchId,
                session.GymClass.Branch.Name,
                sessionVnDate);

            if (!ok || eligibleMembership == null)
                throw new ValidationException(errorReason ?? "Gói tập không đủ điều kiện đặt lớp.");

            if (await _bookingRepository.HasActiveClassBookingAsync(member.Id, session.Id, ct) ||
                await _waitlistEntryRepository.GetActiveEntryAsync(session.Id, member.Id, ct) != null)
            {
                throw new ValidationException("Bạn đã đăng ký hoặc đang trong danh sách chờ của buổi học này.");
            }

            if (await _bookingRepository.HasTimeConflictAsync(member.Id, startAtUtc, endAtUtc, null, ct))
            {
                throw new ValidationException("Khung giờ buổi học bị trùng với một lịch tập khác đã đặt của bạn.");
            }

            if (eligibleMembership.Plan.MaxBookingsPerMonth > 0)
            {
                var usedInMonth = await _bookingRepository.CountMonthlyClassBookingsUsedAsync(
                    member.Id, sessionVnDateTime.Year, sessionVnDateTime.Month, ct);

                var myWaitlists = await _waitlistEntryRepository.GetByMemberIdWithDetailsAsync(member.Id, ct);
                var activeWaitlistInMonth = myWaitlists.Count(w =>
                {
                    if (!w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase)) return false;
                    var wVn = SchedulingHelpers.Utc(w.ClassSession.StartAt).Add(SchedulingHelpers.VietnamOffset);
                    return wVn.Year == sessionVnDateTime.Year && wVn.Month == sessionVnDateTime.Month;
                });

                if (usedInMonth + activeWaitlistInMonth >= eligibleMembership.Plan.MaxBookingsPerMonth)
                {
                    throw new ValidationException(
                        $"Gói '{eligibleMembership.Plan.Name}' giới hạn tối đa {eligibleMembership.Plan.MaxBookingsPerMonth} lượt đặt lớp/tháng. Bạn đã dùng hết hạn mức tháng {sessionVnDateTime.Month}/{sessionVnDateTime.Year}.");
                }
            }

            var currentSeats = await _bookingRepository.CountConfirmedSeatsAsync(session.Id, ct);
            var startLocalText = sessionVnDateTime.ToString("HH:mm dd/MM/yyyy");

            if (currentSeats < session.GymClass.Capacity)
            {
                var booking = new Booking
                {
                    MemberId = member.Id,
                    MembershipId = eligibleMembership.Id,
                    Type = BookingType.Class,
                    ClassSessionId = session.Id,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = nowUtc
                };

                await _bookingRepository.AddAsync(booking, ct);

                var newSeats = currentSeats + 1;
                session.Status = newSeats >= session.GymClass.Capacity ? "FULL" : "SCHEDULED";
                _classSessionRepository.Update(session);

                await _notificationRepository.AddAsync(
                    SchedulingHelpers.BuildNotification(
                        member.UserId,
                        "BOOKING_CONFIRMED",
                        "Đặt lớp thành công",
                        $"Bạn đã đặt chỗ lớp {session.GymClass.Name} lúc {startLocalText} tại {session.GymClass.Room} ({session.GymClass.Branch.Name}).",
                        "Booking",
                        booking.Id),
                    ct);

                await _unitOfWork.SaveChangesAsync(ct);

                var savedBooking = await _bookingRepository.GetByIdWithDetailsAsync(booking.Id, ct) ?? booking;
                return new RegisterClassResponseDto
                {
                    Outcome = "CONFIRMED",
                    Message = $"Đặt lớp {session.GymClass.Name} thành công!",
                    Booking = SchedulingHelpers.MapBookingDto(savedBooking, cancelDeadlineHours)
                };
            }

            // Lớp đã đầy -> kiểm tra mốc T-8h
            var hoursUntilStart = (startAtUtc - nowUtc).TotalHours;
            if (hoursUntilStart <= waitlistCutoffHours)
            {
                throw new ValidationException(
                    $"Lớp đã đầy và chỉ còn dưới {waitlistCutoffHours} giờ trước giờ học nên danh sách chờ đã đóng. Vui lòng chọn buổi học khác.");
            }

            var waitlistEntry = new WaitlistEntry
            {
                ClassSessionId = session.Id,
                MemberId = member.Id,
                PriorityLevel = eligibleMembership.Plan.PriorityLevel,
                RegisteredAt = nowUtc,
                Status = "WAITING"
            };

            await _waitlistEntryRepository.AddAsync(waitlistEntry, ct);

            session.Status = "FULL";
            _classSessionRepository.Update(session);

            await _unitOfWork.SaveChangesAsync(ct);

            var orderedQueue = await _waitlistEntryRepository.GetWaitingBySessionOrderedAsync(session.Id, ct);
            var position = orderedQueue
                .Select((entry, index) => new { entry.Id, Position = index + 1 })
                .FirstOrDefault(x => x.Id == waitlistEntry.Id)?.Position ?? orderedQueue.Count;

            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    member.UserId,
                    "WAITLIST_JOINED",
                    "Đã vào danh sách chờ",
                    $"Lớp {session.GymClass.Name} ({startLocalText}) đã đầy. Bạn đang ở vị trí #{position} trong danh sách chờ (ưu tiên hạng gói: {eligibleMembership.Plan.PriorityLevel}).",
                    "WaitlistEntry",
                    waitlistEntry.Id),
                ct);

            await _unitOfWork.SaveChangesAsync(ct);

            var hydratedEntry = orderedQueue.FirstOrDefault(x => x.Id == waitlistEntry.Id) ?? waitlistEntry;
            hydratedEntry.ClassSession = session;

            return new RegisterClassResponseDto
            {
                Outcome = "WAITLISTED",
                Message = $"Lớp đã đủ sĩ số. Bạn đã được xếp vào danh sách chờ ở vị trí #{position}.",
                WaitlistEntry = SchedulingHelpers.MapWaitlistDto(hydratedEntry, position),
                WaitlistPosition = position
            };
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<CancelBookingResultDto> CancelClassBookingAsync(
        Guid bookingId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var cancelDeadlineHours = GetCancelDeadlineHours();
        var waitlistCutoffHours = GetWaitlistCutoffHours();

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var nowUtc = DateTime.UtcNow;
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, ct)
                ?? throw new NotFoundException($"Không tìm thấy lịch đặt với mã: {bookingId}");

            if (booking.Member.UserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền hủy lịch đặt của người khác.");

            if (booking.Type != BookingType.Class || booking.ClassSession == null)
                throw new ValidationException("Chỉ hỗ trợ hủy lịch đặt lớp học nhóm qua chức năng này.");

            if (booking.Status != BookingStatus.Confirmed)
                throw new ValidationException("Chỉ có thể hủy lịch đặt đang ở trạng thái Đã xác nhận.");

            var session = booking.ClassSession;
            var startAtUtc = SchedulingHelpers.Utc(session.StartAt);
            if (startAtUtc <= nowUtc)
                throw new ValidationException("Buổi học đã bắt đầu hoặc đã kết thúc, không thể hủy.");

            var hoursUntilStart = (startAtUtc - nowUtc).TotalHours;
            var isTimely = hoursUntilStart >= cancelDeadlineHours;

            booking.Status = isTimely ? BookingStatus.Cancelled : BookingStatus.LateCancelled;
            booking.CancelledAt = nowUtc;
            _bookingRepository.Update(booking);

            var startLocalText = startAtUtc.Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy");
            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    booking.Member.UserId,
                    "BOOKING_CANCELLED",
                    "Đã hủy lịch lớp học",
                    isTimely
                        ? $"Bạn đã hủy lớp {session.GymClass.Name} ({startLocalText}) đúng hạn và được hoàn lại lượt đặt."
                        : $"Bạn đã hủy lớp {session.GymClass.Name} ({startLocalText}) sát giờ (dưới {cancelDeadlineHours}h), lượt đặt vẫn được tính là đã sử dụng.",
                    "Booking",
                    booking.Id),
                ct);

            await _unitOfWork.SaveChangesAsync(ct);

            string? promotedMemberName = null;

            // Tự động đôn người ưu tiên cao nhất từ Waitlist nếu còn > T-8h và buổi chưa bị hủy
            if (!session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                !session.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) &&
                hoursUntilStart > waitlistCutoffHours)
            {
                promotedMemberName = await TryPromoteNextFromWaitlistAsync(session, nowUtc, ct);
            }

            var remainingSeats = await _bookingRepository.CountConfirmedSeatsAsync(session.Id, ct);
            if (!session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                !session.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                session.Status = remainingSeats >= session.GymClass.Capacity ? "FULL" : "SCHEDULED";
                _classSessionRepository.Update(session);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var msg = isTimely
                ? $"Đã hủy lịch lớp {session.GymClass.Name} và hoàn lại lượt đặt."
                : $"Đã hủy lịch muộn (dưới {cancelDeadlineHours} giờ). Lượt đặt vẫn tính là đã sử dụng.";

            if (!string.IsNullOrEmpty(promotedMemberName))
                msg += $" Hệ thống đã tự động xếp hội viên {promotedMemberName} từ danh sách chờ vào lớp.";

            return new CancelBookingResultDto
            {
                BookingId = booking.Id,
                Status = booking.Status.ToString(),
                IsTimely = isTimely,
                QuotaRefunded = isTimely,
                PromotedMemberName = promotedMemberName,
                Message = msg
            };
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task CancelWaitlistEntryAsync(
        Guid waitlistEntryId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _waitlistEntryRepository.GetByIdAsync(waitlistEntryId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đăng ký hàng chờ với mã: {waitlistEntryId}");

        var member = await _memberRepository.GetByUserIdAsync(currentUserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ hội viên.");

        if (entry.MemberId != member.Id)
            throw new UnauthorizedAccessException("Bạn không có quyền hủy danh sách chờ của hội viên khác.");

        if (!entry.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Yêu cầu hàng chờ này không còn ở trạng thái đang chờ.");

        entry.Status = "CANCELLED";
        _waitlistEntryRepository.Update(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ProcessWaitlistCutoffAsync(CancellationToken cancellationToken = default)
    {
        var waitlistCutoffHours = GetWaitlistCutoffHours();
        var thresholdUtc = DateTime.UtcNow.AddHours(waitlistCutoffHours);

        var overdueSessions = await _classSessionRepository
            .GetSessionsPastWaitlistCutoffWithWaitingEntriesAsync(thresholdUtc, cancellationToken);

        if (overdueSessions.Count == 0)
            return 0;

        var expiredCount = 0;
        foreach (var session in overdueSessions)
        {
            var startLocalText = SchedulingHelpers.Utc(session.StartAt)
                .Add(SchedulingHelpers.VietnamOffset)
                .ToString("HH:mm dd/MM/yyyy");

            foreach (var entry in session.WaitlistEntries.Where(w => w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase)))
            {
                entry.Status = "EXPIRED";
                _waitlistEntryRepository.Update(entry);
                expiredCount++;

                await _notificationRepository.AddAsync(
                    SchedulingHelpers.BuildNotification(
                        entry.Member.UserId,
                        "WAITLIST_EXPIRED",
                        "Lớp đã đầy — Đóng danh sách chờ",
                        $"Rất tiếc, lớp {session.GymClass.Name} lúc {startLocalText} vẫn đủ sĩ số trước mốc {waitlistCutoffHours} giờ nên hàng chờ đã đóng. Vui lòng chọn lịch học khác.",
                        "ClassSession",
                        session.Id),
                    cancellationToken);
            }
        }

        if (expiredCount > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return expiredCount;
    }

    public async Task<MyScheduleSummaryDto> GetMyScheduleAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await ProcessWaitlistCutoffAsync(cancellationToken);

        var cancelDeadlineHours = GetCancelDeadlineHours();
        var waitlistCutoffHours = GetWaitlistCutoffHours();

        var member = await _memberRepository.GetByUserIdAsync(currentUserId, cancellationToken);
        if (member == null)
        {
            return new MyScheduleSummaryDto
            {
                CancelDeadlineHours = cancelDeadlineHours,
                WaitlistCutoffHours = waitlistCutoffHours
            };
        }

        var nowUtc = DateTime.UtcNow;
        var todayVn = nowUtc.Add(SchedulingHelpers.VietnamOffset).Date;

        var memberships = await _membershipRepository.GetByMemberIdWithPlanAndFreezeAsync(member.Id, cancellationToken);
        var gymMemberships = memberships
            .Where(m => m.Plan != null && m.Plan.PlanType.Equals("MEMBERSHIP", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Plan.PriorityLevel)
            .ThenByDescending(m => m.EndDate)
            .ToList();

        var currentMembership = gymMemberships.FirstOrDefault(m =>
                (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Frozen) &&
                m.StartDate.Date <= todayVn && todayVn <= m.EndDate.Date)
            ?? gymMemberships.FirstOrDefault();

        var isFrozenToday = currentMembership != null && (
            currentMembership.Status == MembershipStatus.Frozen ||
            currentMembership.FreezeRequests.Any(fr =>
                fr.Status == FreezeRequestStatus.Approved &&
                fr.FromDate.Date <= todayVn &&
                todayVn <= fr.ToDate.Date));

        var membershipStatusLabel = currentMembership == null
            ? null
            : isFrozenToday ? "FROZEN" : currentMembership.Status.ToString().ToUpperInvariant();

        var bookings = await _bookingRepository.GetByMemberIdWithDetailsAsync(member.Id, null, cancellationToken);
        var waitlistEntries = await _waitlistEntryRepository.GetByMemberIdWithDetailsAsync(member.Id, cancellationToken);

        var usedThisMonth = await _bookingRepository.CountMonthlyClassBookingsUsedAsync(
            member.Id, todayVn.Year, todayVn.Month, cancellationToken);
        var waitingThisMonth = waitlistEntries.Count(w =>
        {
            if (!w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase)) return false;
            var wVn = SchedulingHelpers.Utc(w.ClassSession.StartAt).Add(SchedulingHelpers.VietnamOffset);
            return wVn.Year == todayVn.Year && wVn.Month == todayVn.Month;
        });

        var mappedBookings = bookings
            .Select(b => SchedulingHelpers.MapBookingDto(b, cancelDeadlineHours))
            .OrderByDescending(b => b.StartAt)
            .ToList();

        var mappedWaitlist = waitlistEntries
            .Where(w => !w.Status.Equals("PROMOTED", StringComparison.OrdinalIgnoreCase))
            .Select(w =>
            {
                var pos = 0;
                if (w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase))
                {
                    var ordered = w.ClassSession.WaitlistEntries
                        .Where(x => x.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(x => x.PriorityLevel)
                        .ThenBy(x => x.RegisteredAt)
                        .ToList();
                    var idx = ordered.FindIndex(x => x.Id == w.Id);
                    pos = idx >= 0 ? idx + 1 : 0;
                }
                return SchedulingHelpers.MapWaitlistDto(w, pos);
            })
            .ToList();

        return new MyScheduleSummaryDto
        {
            ActivePlanName = currentMembership?.Plan?.Name,
            MembershipStatus = membershipStatusLabel,
            PriorityLevel = currentMembership?.Plan?.PriorityLevel ?? 0,
            MaxBookingsPerMonth = currentMembership?.Plan?.MaxBookingsPerMonth ?? 0,
            UsedBookingsThisMonth = usedThisMonth + waitingThisMonth,
            CancelDeadlineHours = cancelDeadlineHours,
            WaitlistCutoffHours = waitlistCutoffHours,
            Bookings = mappedBookings,
            WaitlistEntries = mappedWaitlist
        };
    }

    public async Task<IReadOnlyList<BookingDto>> GetMyClassBookingsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var cancelDeadlineHours = GetCancelDeadlineHours();

        var member = await _memberRepository.GetByUserIdAsync(currentUserId, cancellationToken);
        if (member == null) return [];

        var list = await _bookingRepository.GetByMemberIdWithDetailsAsync(member.Id, BookingType.Class, cancellationToken);
        return list.Select(b => SchedulingHelpers.MapBookingDto(b, cancelDeadlineHours)).ToList();
    }

    public async Task<BookingReviewDto> SubmitBookingReviewAsync(
        Guid bookingId,
        Guid currentUserId,
        CreateBookingReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
            throw new ValidationException("Điểm đánh giá phải từ 1 đến 5 sao.");

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy lượt đặt lịch với mã: {bookingId}");

        if (booking.Member.UserId != currentUserId)
            throw new UnauthorizedAccessException("Bạn chỉ có thể đánh giá buổi học của chính mình.");

        if (booking.Status != BookingStatus.Attended)
            throw new ValidationException("Chỉ được đánh giá sau khi đã được xác nhận tham gia buổi tập.");

        if (booking.Review != null)
            throw new ValidationException("Bạn đã đánh giá buổi tập này trước đó.");

        var review = new Review
        {
            MemberId = booking.MemberId,
            BookingId = booking.Id,
            ClassId = booking.ClassSession?.ClassId,
            TrainerId = booking.ClassSession?.TrainerId ?? booking.Availability?.TrainerId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _reviewRepository.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new BookingReviewDto
        {
            Id = review.Id,
            BookingId = booking.Id,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = SchedulingHelpers.Utc(review.CreatedAt)
        };
    }

    private async Task<string?> TryPromoteNextFromWaitlistAsync(
        ClassSession session,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var seats = await _bookingRepository.CountConfirmedSeatsAsync(session.Id, ct);
        if (seats >= session.GymClass.Capacity)
            return null;

        var waitingQueue = await _waitlistEntryRepository.GetWaitingBySessionOrderedAsync(session.Id, ct);
        if (waitingQueue.Count == 0)
            return null;

        var startAtUtc = SchedulingHelpers.Utc(session.StartAt);
        var endAtUtc = SchedulingHelpers.Utc(session.EndAt);
        var sessionVnDateTime = startAtUtc.Add(SchedulingHelpers.VietnamOffset);
        var sessionVnDate = sessionVnDateTime.Date;
        var startLocalText = sessionVnDateTime.ToString("HH:mm dd/MM/yyyy");

        foreach (var candidate in waitingQueue)
        {
            if (candidate.Member.User.Status == UserStatus.Locked)
                continue;

            var memberships = await _membershipRepository.GetByMemberIdWithPlanAndFreezeAsync(candidate.MemberId, ct);
            var (ok, eligibleMembership, _) = SchedulingHelpers.EvaluateMembershipForClass(
                memberships,
                session.GymClass.BranchId,
                session.GymClass.Branch.Name,
                sessionVnDate);

            if (!ok || eligibleMembership == null)
                continue;

            if (await _bookingRepository.HasActiveClassBookingAsync(candidate.MemberId, session.Id, ct))
                continue;

            if (await _bookingRepository.HasTimeConflictAsync(candidate.MemberId, startAtUtc, endAtUtc, null, ct))
                continue;

            if (eligibleMembership.Plan.MaxBookingsPerMonth > 0)
            {
                var usedInMonth = await _bookingRepository.CountMonthlyClassBookingsUsedAsync(
                    candidate.MemberId, sessionVnDateTime.Year, sessionVnDateTime.Month, ct);
                if (usedInMonth >= eligibleMembership.Plan.MaxBookingsPerMonth)
                    continue;
            }

            candidate.Status = "PROMOTED";
            _waitlistEntryRepository.Update(candidate);

            var promotedBooking = new Booking
            {
                MemberId = candidate.MemberId,
                MembershipId = eligibleMembership.Id,
                Type = BookingType.Class,
                ClassSessionId = session.Id,
                Status = BookingStatus.Confirmed,
                CreatedAt = nowUtc
            };
            await _bookingRepository.AddAsync(promotedBooking, ct);

            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    candidate.Member.UserId,
                    "WAITLIST_PROMOTED",
                    "Bạn đã được xếp vào lớp!",
                    $"Có chỗ trống ở lớp {session.GymClass.Name} lúc {startLocalText} ({session.GymClass.Room}). Bạn đã được tự động chuyển từ danh sách chờ vào lớp chính thức.",
                    "Booking",
                    promotedBooking.Id),
                ct);

            await _unitOfWork.SaveChangesAsync(ct);
            return candidate.Member.User.FullName;
        }

        return null;
    }
}
