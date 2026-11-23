namespace OOAD.Application.DTOs;

public class CheckinDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string? MemberPhone { get; set; }
    public string? CardCode { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid? CardId { get; set; }
    public Guid? BookingId { get; set; }
    public string? AttendedClassName { get; set; }
    public Guid? RecordedBy { get; set; }
    public string? RecordedByName { get; set; }
    public string Method { get; set; } = "QR"; // Card | QR | Manual
    public string? Note { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public bool IsAutoCheckout { get; set; }
    public int? DurationMinutes { get; set; }
}

/// <summary>
/// Kết quả trả về cho Kiosk & Quầy check-in (bao gồm cả trường hợp Hợp lệ lẫn Từ chối kèm lý do).
/// </summary>
public class CheckinOperationResultDto
{
    public bool Allowed { get; set; }
    public string Action { get; set; } = "CHECK_IN"; // CHECK_IN | CHECK_OUT | DENIED
    public string Message { get; set; } = string.Empty;
    public string? DenyCode { get; set; } // CARD_INVALID | CARD_LOCKED | USER_LOCKED | ALREADY_CHECKED_IN | NO_MEMBERSHIP | EXPIRED | FROZEN | WRONG_BRANCH
    public CheckinDto? Checkin { get; set; }

    // Thông tin hội viên + gói tập để hiển thị lên ResultPanel kể cả khi bị từ chối
    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? CardCode { get; set; }
    public string? CardStatus { get; set; }
    public string? PlanName { get; set; }
    public string? MembershipStatus { get; set; }
    public DateTime? PlanEndDate { get; set; }
    public bool AutoAttendedBooking { get; set; }
    public string? AttendedBookingSummary { get; set; }
}

public class CardCheckinRequest
{
    public string CardCode { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    /// <summary>"QR" hoặc "Card" (mặc định "QR").</summary>
    public string Method { get; set; } = "QR";
}

public class ManualCheckinRequest
{
    public Guid MemberId { get; set; }

    public Guid? BranchId { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public class CardCheckoutRequest
{
    public string CardCode { get; set; } = string.Empty;
}

public class ManualCheckoutRequest
{
    public Guid MemberId { get; set; }
}

public class StaffDashboardDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int TodayCheckinCount { get; set; }
    public int TodayManualCheckinCount { get; set; }
    public int PendingFreezeCount { get; set; }
    public int OpenEquipmentIssueCount { get; set; }
    public IReadOnlyList<CheckinDto> PresentMembers { get; set; } = [];
}