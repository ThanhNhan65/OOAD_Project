namespace OOAD.Application.DTOs;

public class ClassSessionDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#7c5cff";
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid TrainerId { get; set; }
    public Guid TrainerUserId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public DateTime SessionDate { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = "SCHEDULED";
    public int BookedCount { get; set; }
    public int AvailableSlots { get; set; }
    public int WaitlistCount { get; set; }
    public bool AttendanceConfirmed { get; set; }
    public int AttendedCount { get; set; }
    public bool CanJoinWaitlist { get; set; }
    public double HoursUntilStart { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    // Ngữ cảnh hội viên đang đăng nhập (nếu có)
    public Guid? MyBookingId { get; set; }
    public string? MyBookingStatus { get; set; }
    public Guid? MyWaitlistEntryId { get; set; }
    public int? MyWaitlistPosition { get; set; }
}

public class ClassSessionDetailDto : ClassSessionDto
{
    public List<SessionRosterItemDto> Roster { get; set; } = new();
    public List<WaitlistEntryDto> Waitlist { get; set; } = new();
    public int CancelDeadlineHours { get; set; } = 2;
    public int WaitlistCutoffHours { get; set; } = 8;
}

public class SessionRosterItemDto
{
    public Guid BookingId { get; set; }
    public Guid MemberId { get; set; }
    public Guid UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string? MemberPhone { get; set; }
    public string? CardCode { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public bool HasCheckedInAtBranch { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime BookedAt { get; set; }
}

public class RescheduleSessionRequest
{
    public DateTime SessionDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string? EndTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Room { get; set; }
    public Guid? TrainerId { get; set; }
}

public class CancelSessionRequest
{
    public string? Reason { get; set; }
}

public class ConfirmAttendanceRequest
{
    public List<Guid>? AttendedBookingIds { get; set; }
    public List<Guid>? AttendedMemberIds { get; set; }
}

public class ConfirmAttendanceResultDto
{
    public Guid SessionId { get; set; }
    public int AttendedCount { get; set; }
    public int NoShowCount { get; set; }
    public int RejectedNotCheckedInCount { get; set; }
    public List<string> RejectedMemberNames { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
