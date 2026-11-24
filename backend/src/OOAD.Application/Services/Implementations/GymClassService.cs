using OOAD.Application.Common.Options;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class GymClassService : IGymClassService
{
    private readonly IGymClassRepository _gymClassRepository;
    private readonly IClassSessionRepository _classSessionRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICheckinRepository _checkinRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ITrainerRepository _trainerRepository;
    private readonly ITrainerAvailabilityRepository _trainerAvailabilityRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly BusinessRuleOptions _ruleOptions;
    private readonly IClassBookingService _classBookingService;
    private readonly IUnitOfWork _unitOfWork;

    public GymClassService(
        IGymClassRepository gymClassRepository,
        IClassSessionRepository classSessionRepository,
        IWaitlistEntryRepository waitlistEntryRepository,
        IBookingRepository bookingRepository,
        ICheckinRepository checkinRepository,
        IBranchRepository branchRepository,
        ITrainerRepository trainerRepository,
        ITrainerAvailabilityRepository trainerAvailabilityRepository,
        IMemberRepository memberRepository,
        INotificationRepository notificationRepository,
        BusinessRuleOptions ruleOptions,
        IClassBookingService classBookingService,
        IUnitOfWork unitOfWork)
    {
        _gymClassRepository = gymClassRepository;
        _classSessionRepository = classSessionRepository;
        _waitlistEntryRepository = waitlistEntryRepository;
        _bookingRepository = bookingRepository;
        _checkinRepository = checkinRepository;
        _branchRepository = branchRepository;
        _trainerRepository = trainerRepository;
        _trainerAvailabilityRepository = trainerAvailabilityRepository;
        _memberRepository = memberRepository;
        _notificationRepository = notificationRepository;
        _ruleOptions = ruleOptions;
        _classBookingService = classBookingService;
        _unitOfWork = unitOfWork;
    }

    private int GetWaitlistCutoffHours() =>
        _ruleOptions.WaitlistCutoffHours > 0 ? _ruleOptions.WaitlistCutoffHours : 8;

    private int GetCancelDeadlineHours() =>
        _ruleOptions.CancelDeadlineHours > 0 ? _ruleOptions.CancelDeadlineHours : 4;

    public async Task<IReadOnlyList<GymClassDto>> GetAllClassesAsync(
        Guid? branchId = null,
        Guid? trainerId = null,
        CancellationToken cancellationToken = default)
    {
        var classes = await _gymClassRepository.GetAllWithDetailsAsync(branchId, trainerId, cancellationToken);
        return classes.Select(MapClassDto).ToList();
    }

    public async Task<GymClassDto> GetClassByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var gymClass = await _gymClassRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy lớp học nhóm với mã: {id}");
        return MapClassDto(gymClass);
    }

    public async Task<GymClassDto> CreateClassAsync(
        CreateGymClassRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Tên lớp học không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Room))
            throw new ValidationException("Phòng tập không được để trống.");
        if (request.Capacity <= 0)
            throw new ValidationException("Sĩ số tối đa phải lớn hơn 0.");

        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy chi nhánh với mã: {request.BranchId}");

        if (!branch.IsActive || !branch.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException($"Chi nhánh {branch.Name} hiện đang tạm ngưng hoạt động.");

        var trainer = await ResolveTrainerAsync(request.TrainerId, cancellationToken);
        if (trainer.User?.Status == UserStatus.Locked)
            throw new ValidationException("Tài khoản Huấn luyện viên đang bị khóa, không thể phân công.");

        var startTime = SchedulingHelpers.ParseTimeSpan(request.StartTime, "Giờ bắt đầu");
        var endTime = ResolveEndTime(startTime, request.EndTime, request.DurationMinutes);
        var days = SchedulingHelpers.NormalizeDaysOfWeek(request.DayOfWeek, request.DaysOfWeek);

        await ValidateClassScheduleConflictsAsync(
            branch.Id, trainer.Id, request.Room.Trim(), days, startTime, endTime, excludeClassId: null, cancellationToken);

        var gymClass = new GymClass
        {
            BranchId = branch.Id,
            TrainerId = trainer.Id,
            Name = request.Name.Trim(),
            Room = request.Room.Trim(),
            Capacity = request.Capacity,
            DayOfWeek = days[0],
            StartTime = startTime,
            EndTime = endTime
        };

        await _gymClassRepository.AddAsync(gymClass, cancellationToken);

        var weeks = Math.Clamp(request.GenerateWeeks, 1, 12);
        var generatedSessions = await BuildNonConflictingSessionsAsync(
            gymClass, trainer.Id, branch.Id, gymClass.Room, days, weeks, cancellationToken);

        foreach (var session in generatedSessions)
            await _classSessionRepository.AddAsync(session, cancellationToken);

        await _notificationRepository.AddAsync(
            SchedulingHelpers.BuildNotification(
                trainer.UserId,
                "CLASS_ASSIGNED",
                "Bạn được phân công lớp học mới",
                $"Lớp {gymClass.Name} tại {branch.Name} ({gymClass.Room}), khung giờ {startTime:hh\\:mm}–{endTime:hh\\:mm} (đã tạo {generatedSessions.Count} buổi học).",
                "GymClass",
                gymClass.Id),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _gymClassRepository.GetByIdWithDetailsAsync(gymClass.Id, cancellationToken) ?? gymClass;
        return MapClassDto(saved);
    }

    public async Task<GymClassDto> UpdateClassAsync(
        Guid id,
        UpdateGymClassRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Tên lớp học không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Room))
            throw new ValidationException("Phòng tập không được để trống.");
        if (request.Capacity <= 0)
            throw new ValidationException("Sĩ số tối đa phải lớn hơn 0.");

        var gymClass = await _gymClassRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy lớp học với mã: {id}");

        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy chi nhánh với mã: {request.BranchId}");

        var newTrainer = await ResolveTrainerAsync(request.TrainerId, cancellationToken);
        if (newTrainer.User?.Status == UserStatus.Locked)
            throw new ValidationException("Tài khoản Huấn luyện viên đang bị khóa.");

        var startTime = SchedulingHelpers.ParseTimeSpan(request.StartTime, "Giờ bắt đầu");
        var endTime = ResolveEndTime(startTime, request.EndTime, request.DurationMinutes);
        var days = SchedulingHelpers.NormalizeDaysOfWeek(request.DayOfWeek, request.DaysOfWeek);

        await ValidateClassScheduleConflictsAsync(
            branch.Id, newTrainer.Id, request.Room.Trim(), days, startTime, endTime, excludeClassId: gymClass.Id, cancellationToken);

        var previousTrainerId = gymClass.TrainerId;
        var previousRoom = gymClass.Room;
        var previousStartTime = gymClass.StartTime;
        var previousEndTime = gymClass.EndTime;

        gymClass.BranchId = branch.Id;
        gymClass.TrainerId = newTrainer.Id;
        gymClass.Name = request.Name.Trim();
        gymClass.Room = request.Room.Trim();
        gymClass.Capacity = request.Capacity;
        gymClass.DayOfWeek = days[0];
        gymClass.StartTime = startTime;
        gymClass.EndTime = endTime;
        _gymClassRepository.Update(gymClass);

        var nowUtc = DateTime.UtcNow;
        var futureSessions = gymClass.Sessions
            .Where(s => SchedulingHelpers.Utc(s.StartAt) > nowUtc &&
                        !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                        !s.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var scheduleChanged = previousRoom != gymClass.Room ||
                              previousStartTime != startTime ||
                              previousEndTime != endTime ||
                              days.Count > 1;

        foreach (var fs in futureSessions)
        {
            var hasActiveSeats = await _bookingRepository.CountConfirmedSeatsAsync(fs.Id, cancellationToken) > 0;
            var hasWaitlist = (await _waitlistEntryRepository.GetWaitingBySessionOrderedAsync(fs.Id, cancellationToken)).Count > 0;

            if (!hasActiveSeats && !hasWaitlist && (request.RegenerateFutureSessions || scheduleChanged))
            {
                _classSessionRepository.Delete(fs);
                gymClass.Sessions.Remove(fs);
            }
            else
            {
                fs.TrainerId = newTrainer.Id;
                _classSessionRepository.Update(fs);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.RegenerateFutureSessions)
        {
            var weeks = Math.Clamp(request.GenerateWeeks, 1, 12);
            var addedSessions = await BuildNonConflictingSessionsAsync(
                gymClass, newTrainer.Id, branch.Id, gymClass.Room, days, weeks, cancellationToken);

            foreach (var session in addedSessions)
                await _classSessionRepository.AddAsync(session, cancellationToken);
        }

        if (previousTrainerId != newTrainer.Id)
        {
            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    newTrainer.UserId,
                    "CLASS_ASSIGNED",
                    "Bạn được phân công phụ trách lớp học",
                    $"Lớp {gymClass.Name} tại {branch.Name} ({gymClass.Room}), {startTime:hh\\:mm}–{endTime:hh\\:mm} đã được phân công cho bạn.",
                    "GymClass",
                    gymClass.Id),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _gymClassRepository.GetByIdWithDetailsAsync(gymClass.Id, cancellationToken) ?? gymClass;
        return MapClassDto(updated);
    }

    public async Task DeleteClassAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var gymClass = await _gymClassRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy lớp học với mã: {id}");

        var nowUtc = DateTime.UtcNow;
        var hasHistory = false;

        foreach (var s in gymClass.Sessions)
        {
            var sessionWithBookings = await _classSessionRepository.GetByIdWithDetailsAsync(s.Id, cancellationToken);
            if (sessionWithBookings == null) continue;

            if (SchedulingHelpers.Utc(sessionWithBookings.StartAt) > nowUtc &&
                !sessionWithBookings.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                !sessionWithBookings.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                await CancelSessionInternalAsync(sessionWithBookings, "Lớp học ngừng hoạt động", cancellationToken);
            }

            if (sessionWithBookings.Bookings.Count > 0)
                hasHistory = true;
        }

        if (!hasHistory && gymClass.Reviews.Count == 0 && gymClass.Sessions.Count == 0)
        {
            _gymClassRepository.Delete(gymClass);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassSessionDto>> GenerateSessionsAsync(
        Guid classId,
        GenerateSessionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var gymClass = await _gymClassRepository.GetByIdWithDetailsAsync(classId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy lớp học với mã: {classId}");

        var days = request.DaysOfWeek != null && request.DaysOfWeek.Count > 0
            ? SchedulingHelpers.NormalizeDaysOfWeek(null, request.DaysOfWeek)
            : new List<int> { gymClass.DayOfWeek == 7 ? 0 : gymClass.DayOfWeek };

        var weeks = Math.Clamp(request.Weeks, 1, 12);
        var createdSessions = await BuildNonConflictingSessionsAsync(
            gymClass, gymClass.TrainerId, gymClass.BranchId, gymClass.Room, days, weeks, cancellationToken);

        foreach (var s in createdSessions)
            await _classSessionRepository.AddAsync(s, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var waitlistCutoffHours = GetWaitlistCutoffHours();

        var result = new List<ClassSessionDto>();
        foreach (var s in createdSessions)
        {
            var hydrated = await _classSessionRepository.GetByIdWithDetailsAsync(s.Id, cancellationToken);
            if (hydrated != null)
                result.Add(MapSessionDto(hydrated, null, waitlistCutoffHours));
        }

        return result;
    }

    public async Task<IReadOnlyList<ClassSessionDto>> GetSessionsAsync(
        Guid? branchId = null,
        Guid? classId = null,
        Guid? trainerId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? status = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        await _classBookingService.ProcessWaitlistCutoffAsync(cancellationToken);

        var waitlistCutoffHours = GetWaitlistCutoffHours();

        Guid? currentMemberId = null;
        if (currentUserId.HasValue)
        {
            var member = await _memberRepository.GetByUserIdAsync(currentUserId.Value, cancellationToken);
            currentMemberId = member?.Id;
        }

        var sessions = await _classSessionRepository.GetSessionsWithDetailsAsync(
            branchId, classId, trainerId, from, to, status, cancellationToken);

        return sessions.Select(s => MapSessionDto(s, currentMemberId, waitlistCutoffHours)).ToList();
    }

    public async Task<ClassSessionDetailDto> GetSessionDetailAsync(
        Guid sessionId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        await _classBookingService.ProcessWaitlistCutoffAsync(cancellationToken);

        var session = await _classSessionRepository.GetByIdWithDetailsAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy buổi học với mã: {sessionId}");

        Guid? currentMemberId = null;
        if (currentUserId.HasValue)
        {
            var member = await _memberRepository.GetByUserIdAsync(currentUserId.Value, cancellationToken);
            currentMemberId = member?.Id;
        }

        var cancelDeadlineHours = GetCancelDeadlineHours();
        var waitlistCutoffHours = GetWaitlistCutoffHours();

        var baseDto = MapSessionDto(session, currentMemberId, waitlistCutoffHours);

        var activeBookings = session.Bookings
            .Where(b =>
                b.Status == BookingStatus.Confirmed ||
                b.Status == BookingStatus.Attended ||
                b.Status == BookingStatus.NoShow)
            .OrderBy(b => b.CreatedAt)
            .ToList();

        var sessionVnDate = SchedulingHelpers.ToVietnamDate(session.StartAt);
        var dayFromUtc = sessionVnDate.Subtract(SchedulingHelpers.VietnamOffset);
        var dayToUtc = sessionVnDate.AddDays(1).Subtract(SchedulingHelpers.VietnamOffset);

        var checkedInTimes = await _checkinRepository.GetCheckedInMemberTimesOnDateAsync(
            session.GymClass.BranchId,
            dayFromUtc,
            dayToUtc,
            activeBookings.Select(b => b.MemberId),
            cancellationToken);

        var roster = activeBookings.Select(b =>
        {
            var activeCard = b.Member.MemberCards
                .FirstOrDefault(c => c.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
                ?? b.Member.MemberCards.FirstOrDefault();

            var hasCheckin = b.Checkin != null || checkedInTimes.ContainsKey(b.MemberId);
            DateTime? checkInAt = b.Checkin != null
                ? SchedulingHelpers.Utc(b.Checkin.CheckInAt)
                : checkedInTimes.TryGetValue(b.MemberId, out var ts) ? SchedulingHelpers.Utc(ts) : null;

            return new SessionRosterItemDto
            {
                BookingId = b.Id,
                MemberId = b.MemberId,
                UserId = b.Member.UserId,
                MemberName = b.Member.User.FullName,
                MemberEmail = b.Member.User.Email,
                MemberPhone = b.Member.User.Phone,
                CardCode = activeCard?.CardCode,
                BookingStatus = b.Status.ToString(),
                HasCheckedInAtBranch = hasCheckin,
                CheckInAt = checkInAt,
                BookedAt = SchedulingHelpers.Utc(b.CreatedAt)
            };
        }).ToList();

        var orderedWaitlist = session.WaitlistEntries
            .Where(w => w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(w => w.PriorityLevel)
            .ThenBy(w => w.RegisteredAt)
            .Select((w, idx) => SchedulingHelpers.MapWaitlistDto(w, idx + 1))
            .ToList();

        return new ClassSessionDetailDto
        {
            Id = baseDto.Id,
            ClassId = baseDto.ClassId,
            ClassName = baseDto.ClassName,
            Category = baseDto.Category,
            Color = baseDto.Color,
            BranchId = baseDto.BranchId,
            BranchName = baseDto.BranchName,
            TrainerId = baseDto.TrainerId,
            TrainerUserId = baseDto.TrainerUserId,
            TrainerName = baseDto.TrainerName,
            Room = baseDto.Room,
            Capacity = baseDto.Capacity,
            SessionDate = baseDto.SessionDate,
            StartAt = baseDto.StartAt,
            EndAt = baseDto.EndAt,
            DurationMinutes = baseDto.DurationMinutes,
            Status = baseDto.Status,
            BookedCount = baseDto.BookedCount,
            AvailableSlots = baseDto.AvailableSlots,
            WaitlistCount = baseDto.WaitlistCount,
            AttendanceConfirmed = baseDto.AttendanceConfirmed,
            AttendedCount = baseDto.AttendedCount,
            CanJoinWaitlist = baseDto.CanJoinWaitlist,
            HoursUntilStart = baseDto.HoursUntilStart,
            AverageRating = baseDto.AverageRating,
            ReviewCount = baseDto.ReviewCount,
            MyBookingId = baseDto.MyBookingId,
            MyBookingStatus = baseDto.MyBookingStatus,
            MyWaitlistEntryId = baseDto.MyWaitlistEntryId,
            MyWaitlistPosition = baseDto.MyWaitlistPosition,
            Roster = roster,
            Waitlist = orderedWaitlist,
            CancelDeadlineHours = cancelDeadlineHours,
            WaitlistCutoffHours = waitlistCutoffHours
        };
    }

    public async Task<IReadOnlyList<ClassSessionDto>> GetTrainerSessionsAsync(
        Guid currentUserId,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        await _classBookingService.ProcessWaitlistCutoffAsync(cancellationToken);

        var trainer = await _trainerRepository.GetByUserIdAsync(currentUserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ Huấn luyện viên cho tài khoản hiện tại.");

        var sessions = await _classSessionRepository.GetSessionsWithDetailsAsync(
            branchId: null,
            classId: null,
            trainerId: trainer.Id,
            fromUtc: null,
            toUtc: null,
            status: null,
            cancellationToken: cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var filtered = (filter ?? "all").Trim().ToLowerInvariant() switch
        {
            "upcoming" => sessions.Where(s =>
                !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                !s.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) &&
                SchedulingHelpers.Utc(s.StartAt) > nowUtc),
            "tomark" or "pending" => sessions.Where(s =>
                !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
                !s.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) &&
                SchedulingHelpers.Utc(s.StartAt) <= nowUtc)
                .OrderByDescending(s => s.StartAt),
            "done" or "completed" => sessions.Where(s =>
                s.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.StartAt),
            _ => sessions.AsEnumerable()
        };

        return filtered.Select(s => MapSessionDto(s, null)).ToList();
    }

    public async Task<ClassSessionDto> RescheduleSessionAsync(
        Guid sessionId,
        RescheduleSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _classSessionRepository.GetByIdWithDetailsAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy buổi học với mã: {sessionId}");

        if (session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
            session.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Không thể đổi lịch buổi học đã hủy hoặc đã hoàn thành.");
        }

        var startTime = SchedulingHelpers.ParseTimeSpan(request.StartTime, "Giờ bắt đầu mới");
        var defaultDuration = Math.Max(15, (int)Math.Round((session.EndAt - session.StartAt).TotalMinutes));
        var endTime = ResolveEndTime(startTime, request.EndTime, request.DurationMinutes ?? defaultDuration);

        var newDateVn = request.SessionDate.Date;
        var newStartUtc = newDateVn.Add(startTime).Subtract(SchedulingHelpers.VietnamOffset);
        var newEndUtc = newDateVn.Add(endTime).Subtract(SchedulingHelpers.VietnamOffset);

        if (newStartUtc <= DateTime.UtcNow)
            throw new ValidationException("Thời gian học mới phải nằm ở tương lai.");

        var targetRoom = string.IsNullOrWhiteSpace(request.Room)
            ? session.GymClass.Room
            : request.Room.Trim();

        var previousTrainerUserId = session.Trainer.UserId;
        var targetTrainer = request.TrainerId.HasValue && request.TrainerId.Value != Guid.Empty
            ? await ResolveTrainerAsync(request.TrainerId.Value, cancellationToken)
            : session.Trainer;

        if (targetTrainer.User?.Status == UserStatus.Locked)
            throw new ValidationException("Huấn luyện viên được chọn hiện đang bị khóa tài khoản.");

        var sameClassSessions = await _classSessionRepository.GetSessionsWithDetailsAsync(
            classId: session.ClassId,
            fromUtc: newStartUtc,
            toUtc: newEndUtc,
            cancellationToken: cancellationToken);

        var sameClassConflict = sameClassSessions.FirstOrDefault(other =>
            other.Id != session.Id &&
            !other.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
            SchedulingHelpers.Utc(other.StartAt) < newEndUtc &&
            newStartUtc < SchedulingHelpers.Utc(other.EndAt));

        if (sameClassConflict != null)
        {
            throw new ValidationException($"Lớp '{session.GymClass.Name}' đã có buổi học khác trùng khung giờ này.");
        }

        var branchSessions = await _classSessionRepository.GetSessionsWithDetailsAsync(
            branchId: session.GymClass.BranchId,
            fromUtc: newStartUtc,
            toUtc: newEndUtc,
            cancellationToken: cancellationToken);

        var roomConflict = branchSessions.FirstOrDefault(other =>
            other.Id != session.Id &&
            !other.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
            other.GymClass.Room.Trim().Equals(targetRoom, StringComparison.OrdinalIgnoreCase) &&
            SchedulingHelpers.Utc(other.StartAt) < newEndUtc &&
            newStartUtc < SchedulingHelpers.Utc(other.EndAt));

        if (roomConflict != null)
        {
            var otherStartVn = SchedulingHelpers.Utc(roomConflict.StartAt).Add(SchedulingHelpers.VietnamOffset);
            var otherEndVn = SchedulingHelpers.Utc(roomConflict.EndAt).Add(SchedulingHelpers.VietnamOffset);
            throw new ValidationException(
                $"Phòng '{targetRoom}' tại {session.GymClass.Branch.Name} đã có lớp '{roomConflict.GymClass.Name}' ({otherStartVn:HH:mm}–{otherEndVn:HH:mm}) trùng khung giờ.");
        }

        if (!session.GymClass.Room.Trim().Equals(targetRoom, StringComparison.OrdinalIgnoreCase))
        {
            var otherClassSessions = await _classSessionRepository.GetSessionsWithDetailsAsync(
                classId: session.ClassId,
                fromUtc: DateTime.UtcNow,
                cancellationToken: cancellationToken);

            foreach (var other in otherClassSessions)
            {
                if (other.Id == session.Id ||
                    other.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                    other.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherStartUtc = SchedulingHelpers.Utc(other.StartAt);
                var otherEndUtc = SchedulingHelpers.Utc(other.EndAt);
                if (await _classSessionRepository.HasRoomConflictAsync(
                    session.GymClass.BranchId, targetRoom, otherStartUtc, otherEndUtc, other.Id, cancellationToken))
                {
                    var otherStartVn = otherStartUtc.Add(SchedulingHelpers.VietnamOffset);
                    throw new ValidationException(
                        $"Không thể đổi phòng lớp sang '{targetRoom}' vì buổi học khác của lớp ({otherStartVn:HH:mm dd/MM/yyyy}) bị trùng phòng với lớp khác.");
                }
            }
        }

        var trainerSessions = await _classSessionRepository.GetSessionsWithDetailsAsync(
            trainerId: targetTrainer.Id,
            fromUtc: newStartUtc,
            toUtc: newEndUtc,
            cancellationToken: cancellationToken);

        var trainerConflict = trainerSessions.FirstOrDefault(other =>
            other.Id != session.Id &&
            !other.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) &&
            SchedulingHelpers.Utc(other.StartAt) < newEndUtc &&
            newStartUtc < SchedulingHelpers.Utc(other.EndAt));

        if (trainerConflict != null)
        {
            var otherStartVn = SchedulingHelpers.Utc(trainerConflict.StartAt).Add(SchedulingHelpers.VietnamOffset);
            var otherEndVn = SchedulingHelpers.Utc(trainerConflict.EndAt).Add(SchedulingHelpers.VietnamOffset);
            throw new ValidationException(
                $"Huấn luyện viên {targetTrainer.User?.FullName} đã có lịch dạy lớp '{trainerConflict.GymClass.Name}' tại {trainerConflict.GymClass.Branch.Name} ({otherStartVn:HH:mm}–{otherEndVn:HH:mm}) trùng khung giờ.");
        }

        if (await HasBookedPtConflictAsync(targetTrainer.Id, newStartUtc, newEndUtc, cancellationToken))
        {
            throw new ValidationException($"Huấn luyện viên {targetTrainer.User?.FullName} đã có lịch hẹn PT 1-kèm-1 trùng khung giờ.");
        }

        var oldTimeText = SchedulingHelpers.Utc(session.StartAt).Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy");
        var newTimeText = newStartUtc.Add(SchedulingHelpers.VietnamOffset).ToString("HH:mm dd/MM/yyyy");

        session.SessionDate = newDateVn;
        session.StartAt = newStartUtc;
        session.EndAt = newEndUtc;
        session.TrainerId = targetTrainer.Id;
        _classSessionRepository.Update(session);

        if (!session.GymClass.Room.Trim().Equals(targetRoom, StringComparison.OrdinalIgnoreCase))
        {
            session.GymClass.Room = targetRoom;
            _gymClassRepository.Update(session.GymClass);
        }

        var changeMsg = $"Buổi học {session.GymClass.Name} đã được đổi lịch từ {oldTimeText} sang {newTimeText} tại {targetRoom} ({session.GymClass.Branch.Name}).";

        foreach (var b in session.Bookings.Where(x => x.Status == BookingStatus.Confirmed))
        {
            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    b.Member.UserId,
                    "SESSION_RESCHEDULED",
                    "Buổi học được đổi lịch",
                    changeMsg,
                    "ClassSession",
                    session.Id),
                cancellationToken);
        }

        foreach (var w in session.WaitlistEntries.Where(x => x.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase)))
        {
            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    w.Member.UserId,
                    "SESSION_RESCHEDULED",
                    "Buổi học đang chờ được đổi lịch",
                    changeMsg,
                    "ClassSession",
                    session.Id),
                cancellationToken);
        }

        await _notificationRepository.AddAsync(
            SchedulingHelpers.BuildNotification(
                targetTrainer.UserId,
                "SESSION_RESCHEDULED",
                "Lịch dạy buổi học đã thay đổi",
                changeMsg,
                "ClassSession",
                session.Id),
            cancellationToken);

        if (previousTrainerUserId != targetTrainer.UserId)
        {
            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    previousTrainerUserId,
                    "SESSION_REASSIGNED",
                    "Buổi dạy đã chuyển cho HLV khác",
                    $"Buổi học {session.GymClass.Name} lúc {oldTimeText} đã được phân công cho HLV khác.",
                    "ClassSession",
                    session.Id),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _classSessionRepository.GetByIdWithDetailsAsync(session.Id, cancellationToken) ?? session;
        return MapSessionDto(updated, null);
    }

    public async Task<ClassSessionDto> CancelSessionAsync(
        Guid sessionId,
        CancelSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _classSessionRepository.GetByIdWithDetailsAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy buổi học với mã: {sessionId}");

        if (session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Buổi học này đã được hủy trước đó.");

        if (session.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Không thể hủy buổi học đã hoàn thành.");

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Lịch học thay đổi hoặc sự cố vận hành phòng tập"
            : request.Reason.Trim();

        await CancelSessionInternalAsync(session, reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _classSessionRepository.GetByIdWithDetailsAsync(session.Id, cancellationToken) ?? session;
        return MapSessionDto(updated, null);
    }

    public async Task<ConfirmAttendanceResultDto> ConfirmAttendanceAsync(
        Guid sessionId,
        Guid currentUserId,
        string currentRole,
        ConfirmAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _classSessionRepository.GetByIdWithDetailsAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy buổi học với mã: {sessionId}");

        if (!currentRole.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            var trainer = await _trainerRepository.GetByUserIdAsync(currentUserId, cancellationToken);
            if (trainer == null || trainer.Id != session.TrainerId)
                throw new UnauthorizedAccessException("Bạn chỉ được phép điểm danh các buổi học do mình phụ trách.");
        }

        if (session.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Không thể điểm danh buổi học đã bị hủy.");

        var nowUtc = DateTime.UtcNow;
        if (SchedulingHelpers.Utc(session.StartAt) > nowUtc)
            throw new ValidationException("Chỉ có thể điểm danh sau khi buổi học đã bắt đầu.");

        var targetBookings = session.Bookings
            .Where(b =>
                b.Status == BookingStatus.Confirmed ||
                b.Status == BookingStatus.Attended ||
                b.Status == BookingStatus.NoShow)
            .ToList();

        var selectedBookingIds = (request.AttendedBookingIds ?? new List<Guid>()).ToHashSet();
        var selectedMemberIds = (request.AttendedMemberIds ?? new List<Guid>()).ToHashSet();

        var sessionVnDate = SchedulingHelpers.ToVietnamDate(session.StartAt);
        var dayFromUtc = sessionVnDate.Subtract(SchedulingHelpers.VietnamOffset);
        var dayToUtc = sessionVnDate.AddDays(1).Subtract(SchedulingHelpers.VietnamOffset);

        var checkedInTimes = await _checkinRepository.GetCheckedInMemberTimesOnDateAsync(
            session.GymClass.BranchId,
            dayFromUtc,
            dayToUtc,
            targetBookings.Select(b => b.MemberId),
            cancellationToken);

        var attendedCount = 0;
        var noShowCount = 0;
        var rejectedNames = new List<string>();

        foreach (var booking in targetBookings)
        {
            var previousStatus = booking.Status;
            var isSelected = selectedBookingIds.Contains(booking.Id) || selectedMemberIds.Contains(booking.MemberId);
            var hasValidCheckin = booking.Checkin != null || checkedInTimes.ContainsKey(booking.MemberId);

            if (isSelected && hasValidCheckin)
            {
                booking.Status = BookingStatus.Attended;
                attendedCount++;

                if (previousStatus != BookingStatus.Attended)
                {
                    await _notificationRepository.AddAsync(
                        SchedulingHelpers.BuildNotification(
                            booking.Member.UserId,
                            "ATTENDANCE_CONFIRMED",
                            "Đã xác nhận tham gia buổi học",
                            $"Bạn đã được xác nhận tham gia lớp {session.GymClass.Name}. Hãy để lại đánh giá trải nghiệm buổi tập nhé!",
                            "Booking",
                            booking.Id),
                        cancellationToken);
                }
            }
            else
            {
                if (isSelected && !hasValidCheckin)
                    rejectedNames.Add(booking.Member.User.FullName);

                booking.Status = BookingStatus.NoShow;
                noShowCount++;
            }

            _bookingRepository.Update(booking);
        }

        session.Status = "COMPLETED";
        _classSessionRepository.Update(session);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var message = rejectedNames.Count > 0
            ? $"Đã lưu điểm danh ({attendedCount} có mặt, {noShowCount} vắng). {rejectedNames.Count} hội viên chưa check-in tại chi nhánh bị ghi vắng ({string.Join(", ", rejectedNames)})."
            : $"Đã xác nhận điểm danh thành công: {attendedCount} có mặt, {noShowCount} vắng.";

        return new ConfirmAttendanceResultDto
        {
            SessionId = session.Id,
            AttendedCount = attendedCount,
            NoShowCount = noShowCount,
            RejectedNotCheckedInCount = rejectedNames.Count,
            RejectedMemberNames = rejectedNames,
            Message = message
        };
    }

    private async Task CancelSessionInternalAsync(
        ClassSession session,
        string reason,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var startLocalText = SchedulingHelpers.Utc(session.StartAt)
            .Add(SchedulingHelpers.VietnamOffset)
            .ToString("HH:mm dd/MM/yyyy");

        session.Status = "CANCELLED";
        _classSessionRepository.Update(session);

        foreach (var booking in session.Bookings.Where(b => b.Status == BookingStatus.Confirmed))
        {
            booking.Status = BookingStatus.AutoCancelled;
            booking.CancelledAt = nowUtc;
            _bookingRepository.Update(booking);

            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    booking.Member.UserId,
                    "SESSION_CANCELLED",
                    "Buổi học bị hủy — Đã hoàn lượt",
                    $"Lớp {session.GymClass.Name} lúc {startLocalText} đã bị hủy ({reason}). Lượt đặt của bạn đã được hoàn lại.",
                    "ClassSession",
                    session.Id),
                cancellationToken);
        }

        foreach (var entry in session.WaitlistEntries.Where(w => w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase)))
        {
            entry.Status = "CANCELLED";
            _waitlistEntryRepository.Update(entry);

            await _notificationRepository.AddAsync(
                SchedulingHelpers.BuildNotification(
                    entry.Member.UserId,
                    "SESSION_CANCELLED",
                    "Buổi học trong danh sách chờ đã hủy",
                    $"Lớp {session.GymClass.Name} lúc {startLocalText} đã bị hủy ({reason}).",
                    "ClassSession",
                    session.Id),
                cancellationToken);
        }

        await _notificationRepository.AddAsync(
            SchedulingHelpers.BuildNotification(
                session.Trainer.UserId,
                "SESSION_CANCELLED",
                "Buổi dạy bị hủy",
                $"Buổi dạy lớp {session.GymClass.Name} lúc {startLocalText} đã bị hủy ({reason}).",
                "ClassSession",
                session.Id),
            cancellationToken);
    }

    private async Task<Trainer> ResolveTrainerAsync(Guid trainerOrUserId, CancellationToken cancellationToken)
    {
        var trainer = await _trainerRepository.GetWithDetailsAsync(trainerOrUserId, cancellationToken)
            ?? await _trainerRepository.GetByUserIdAsync(trainerOrUserId, cancellationToken);

        return trainer ?? throw new NotFoundException($"Không tìm thấy Huấn luyện viên với mã: {trainerOrUserId}");
    }

    private static TimeSpan ResolveEndTime(TimeSpan startTime, string? endTimeStr, int? durationMinutes)
    {
        TimeSpan endTime;
        if (!string.IsNullOrWhiteSpace(endTimeStr))
        {
            endTime = SchedulingHelpers.ParseTimeSpan(endTimeStr, "Giờ kết thúc");
        }
        else
        {
            var mins = durationMinutes ?? 60;
            if (mins < 15)
                throw new ValidationException("Thời lượng lớp học tối thiểu là 15 phút.");
            endTime = startTime.Add(TimeSpan.FromMinutes(mins));
        }

        if (endTime <= startTime || endTime >= TimeSpan.FromHours(24))
            throw new ValidationException("Giờ kết thúc phải sau giờ bắt đầu và nằm trong cùng ngày.");

        return endTime;
    }

    private async Task ValidateClassScheduleConflictsAsync(
        Guid branchId,
        Guid trainerId,
        string room,
        IReadOnlyList<int> days,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeClassId,
        CancellationToken cancellationToken)
    {
        foreach (var day in days)
        {
            if (await _gymClassRepository.HasRoomConflictAsync(branchId, room, day, startTime, endTime, excludeClassId, cancellationToken))
            {
                throw new ValidationException($"Phòng '{room}' bị trùng lịch với lớp khác trong khung giờ {startTime:hh\\:mm}–{endTime:hh\\:mm}.");
            }

            if (await _gymClassRepository.HasTrainerClassConflictAsync(trainerId, day, startTime, endTime, excludeClassId, cancellationToken))
            {
                throw new ValidationException($"Huấn luyện viên đã có lớp dạy khác trùng khung giờ {startTime:hh\\:mm}–{endTime:hh\\:mm}.");
            }
        }
    }

    private async Task<List<ClassSession>> BuildNonConflictingSessionsAsync(
        GymClass gymClass,
        Guid trainerId,
        Guid branchId,
        string room,
        IReadOnlyList<int> days,
        int weeks,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var todayVn = nowUtc.Add(SchedulingHelpers.VietnamOffset).Date;
        var totalDays = weeks * 7;

        var existingStartAts = gymClass.Sessions
            .Where(s => !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            .Select(s => SchedulingHelpers.Utc(s.StartAt))
            .ToHashSet();

        var created = new List<ClassSession>();

        for (int offset = 0; offset <= totalDays; offset++)
        {
            var dateVn = todayVn.AddDays(offset);
            if (!days.Contains((int)dateVn.DayOfWeek))
                continue;

            var startUtc = dateVn.Add(gymClass.StartTime).Subtract(SchedulingHelpers.VietnamOffset);
            var endUtc = dateVn.Add(gymClass.EndTime).Subtract(SchedulingHelpers.VietnamOffset);

            if (startUtc <= nowUtc)
                continue;

            if (existingStartAts.Contains(startUtc))
                continue;

            if (await _classSessionRepository.HasRoomConflictAsync(branchId, room, startUtc, endUtc, null, cancellationToken))
                throw new ValidationException($"Phòng '{room}' bị trùng lịch vào ngày {dateVn:dd/MM/yyyy} ({gymClass.StartTime:hh\\:mm}–{gymClass.EndTime:hh\\:mm}).");

            if (await _classSessionRepository.HasTrainerSessionConflictAsync(trainerId, startUtc, endUtc, null, cancellationToken))
                throw new ValidationException($"Huấn luyện viên bị trùng buổi dạy vào ngày {dateVn:dd/MM/yyyy} ({gymClass.StartTime:hh\\:mm}–{gymClass.EndTime:hh\\:mm}).");

            if (await HasBookedPtConflictAsync(trainerId, startUtc, endUtc, cancellationToken))
                throw new ValidationException($"Huấn luyện viên đã có lịch hẹn PT 1-kèm-1 vào ngày {dateVn:dd/MM/yyyy} ({gymClass.StartTime:hh\\:mm}–{gymClass.EndTime:hh\\:mm}).");

            created.Add(new ClassSession
            {
                ClassId = gymClass.Id,
                TrainerId = trainerId,
                SessionDate = dateVn,
                StartAt = startUtc,
                EndAt = endUtc,
                Status = "SCHEDULED"
            });
        }

        return created;
    }

    private async Task<bool> HasBookedPtConflictAsync(
        Guid trainerId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var slots = await _trainerAvailabilityRepository.GetByTrainerIdAsync(trainerId, cancellationToken);
        return slots.Any(a =>
            a.IsBooked &&
            SchedulingHelpers.Utc(a.StartAt) < endUtc &&
            startUtc < SchedulingHelpers.Utc(a.EndAt));
    }

    private static GymClassDto MapClassDto(GymClass c)
    {
        var nowUtc = DateTime.UtcNow;
        var (category, color) = SchedulingHelpers.InferCategoryAndColor(c.Name, c.Room);
        var duration = Math.Max(15, (int)Math.Round((c.EndTime - c.StartTime).TotalMinutes));

        var upcomingSessions = c.Sessions?
            .Where(s =>
                SchedulingHelpers.Utc(s.StartAt) > nowUtc &&
                !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? new List<ClassSession>();

        var daysOfWeek = (c.Sessions ?? Array.Empty<ClassSession>())
            .Where(s => !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
            .Select(s => (int)s.SessionDate.DayOfWeek)
            .Append(c.DayOfWeek == 7 ? 0 : c.DayOfWeek)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var reviews = c.Reviews?.ToList() ?? new List<Review>();
        var avgRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 5.0;

        var hasAnySessions = c.Sessions != null && c.Sessions.Count > 0;
        var hasOnlyCancelledFuture = hasAnySessions &&
            c.Sessions!.All(s => s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase));

        return new GymClassDto
        {
            Id = c.Id,
            BranchId = c.BranchId,
            BranchName = c.Branch?.Name ?? string.Empty,
            TrainerId = c.TrainerId,
            TrainerUserId = c.Trainer?.UserId ?? Guid.Empty,
            TrainerName = c.Trainer?.User?.FullName ?? "Chưa phân công",
            TrainerSpecialty = c.Trainer?.Specialty,
            Name = c.Name,
            Room = c.Room,
            Capacity = c.Capacity,
            DayOfWeek = c.DayOfWeek == 7 ? 0 : c.DayOfWeek,
            DaysOfWeek = daysOfWeek,
            StartTime = c.StartTime.ToString(@"hh\:mm"),
            EndTime = c.EndTime.ToString(@"hh\:mm"),
            DurationMinutes = duration,
            Category = category,
            Color = color,
            UpcomingSessionCount = upcomingSessions.Count,
            Active = !hasOnlyCancelledFuture,
            AverageRating = avgRating,
            ReviewCount = reviews.Count
        };
    }

    private static ClassSessionDto MapSessionDto(ClassSession s, Guid? currentMemberId, int waitlistCutoffHours = 8)
    {
        var nowUtc = DateTime.UtcNow;
        var gc = s.GymClass;
        var (category, color) = SchedulingHelpers.InferCategoryAndColor(gc.Name, gc.Room);

        var startAt = SchedulingHelpers.Utc(s.StartAt);
        var endAt = SchedulingHelpers.Utc(s.EndAt);
        var duration = Math.Max(15, (int)Math.Round((endAt - startAt).TotalMinutes));

        var activeSeatBookings = s.Bookings?
            .Where(b =>
                b.Status == BookingStatus.Confirmed ||
                b.Status == BookingStatus.Attended ||
                b.Status == BookingStatus.NoShow)
            .ToList() ?? new List<Booking>();

        var bookedCount = activeSeatBookings.Count;
        var attendedCount = activeSeatBookings.Count(b => b.Status == BookingStatus.Attended);
        var availableSlots = Math.Max(0, gc.Capacity - bookedCount);

        var waitingEntries = s.WaitlistEntries?
            .Where(w => w.Status.Equals("WAITING", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(w => w.PriorityLevel)
            .ThenBy(w => w.RegisteredAt)
            .ToList() ?? new List<WaitlistEntry>();

        var hoursUntilStart = (startAt - nowUtc).TotalHours;
        var isCancelled = s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase);
        var isCompleted = s.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);

        var canJoinWaitlist = !isCancelled && !isCompleted && bookedCount >= gc.Capacity && hoursUntilStart > waitlistCutoffHours;

        var reviews = gc.Reviews?.ToList() ?? new List<Review>();
        var avgRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 5.0;

        Booking? myBooking = null;
        WaitlistEntry? myWaitlist = null;
        int? myWaitlistPos = null;

        if (currentMemberId.HasValue)
        {
            myBooking = activeSeatBookings.FirstOrDefault(b => b.MemberId == currentMemberId.Value);
            myWaitlist = waitingEntries.FirstOrDefault(w => w.MemberId == currentMemberId.Value);
            if (myWaitlist != null)
            {
                var idx = waitingEntries.FindIndex(w => w.Id == myWaitlist.Id);
                myWaitlistPos = idx >= 0 ? idx + 1 : null;
            }
        }

        return new ClassSessionDto
        {
            Id = s.Id,
            ClassId = s.ClassId,
            ClassName = gc.Name,
            Category = category,
            Color = color,
            BranchId = gc.BranchId,
            BranchName = gc.Branch?.Name ?? string.Empty,
            TrainerId = s.TrainerId,
            TrainerUserId = s.Trainer?.UserId ?? Guid.Empty,
            TrainerName = s.Trainer?.User?.FullName ?? string.Empty,
            Room = gc.Room,
            Capacity = gc.Capacity,
            SessionDate = s.SessionDate,
            StartAt = startAt,
            EndAt = endAt,
            DurationMinutes = duration,
            Status = s.Status.ToUpperInvariant(),
            BookedCount = bookedCount,
            AvailableSlots = availableSlots,
            WaitlistCount = waitingEntries.Count,
            AttendanceConfirmed = isCompleted,
            AttendedCount = attendedCount,
            CanJoinWaitlist = canJoinWaitlist,
            HoursUntilStart = Math.Round(hoursUntilStart, 2),
            AverageRating = avgRating,
            ReviewCount = reviews.Count,
            MyBookingId = myBooking?.Id,
            MyBookingStatus = myBooking?.Status.ToString(),
            MyWaitlistEntryId = myWaitlist?.Id,
            MyWaitlistPosition = myWaitlistPos
        };
    }
}
