namespace OOAD.Application.DTOs;

public class BookingDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid MembershipId { get; set; }
    public string Type { get; set; } = "Class";
    public Guid? ClassSessionId { get; set; }
    public Guid? AvailabilityId { get; set; }
    public Guid? ClassId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#7c5cff";
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public Guid TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public bool CanCancel { get; set; }
    public bool IsCancelTimely { get; set; }
    public int CancelDeadlineHours { get; set; } = 2;
    public bool CanReview { get; set; }
    public bool IsReviewed { get; set; }
    public bool CheckedIn { get; set; }
}

public class BookClassRequest
{
    public Guid ClassSessionId { get; set; }
}

public class RegisterClassResponseDto
{
    public string Outcome { get; set; } = "CONFIRMED"; // "CONFIRMED" | "WAITLISTED"
    public string Message { get; set; } = string.Empty;
    public BookingDto? Booking { get; set; }
    public WaitlistEntryDto? WaitlistEntry { get; set; }
    public int? WaitlistPosition { get; set; }
}

public class CancelBookingResultDto
{
    public Guid BookingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsTimely { get; set; }
    public bool QuotaRefunded { get; set; }
    public string? PromotedMemberName { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MyScheduleSummaryDto
{
    public string? ActivePlanName { get; set; }
    public string? MembershipStatus { get; set; }
    public int PriorityLevel { get; set; }
    public int MaxBookingsPerMonth { get; set; }
    public int UsedBookingsThisMonth { get; set; }
    public int CancelDeadlineHours { get; set; } = 2;
    public int WaitlistCutoffHours { get; set; } = 8;
    public List<BookingDto> Bookings { get; set; } = new();
    public List<WaitlistEntryDto> WaitlistEntries { get; set; } = new();
}

public class CreateBookingReviewRequest
{
    public int Rating { get; set; } = 5;
    public string? Comment { get; set; }
}

public class BookingReviewDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
