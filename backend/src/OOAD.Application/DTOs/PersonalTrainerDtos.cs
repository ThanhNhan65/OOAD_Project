namespace OOAD.Application.DTOs;

public class BookPtRequest
{
    public Guid AvailabilityId { get; set; }
    public string? Note { get; set; }
    public Guid? MembershipId { get; set; }
}

public class PtBookingDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? MemberPhone { get; set; }
    public Guid TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string? TrainerSpecialty { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid AvailabilityId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty; // PENDING, CONFIRMED, ATTENDED, CANCELLED
    public string? Note { get; set; }
    public string SourceType { get; set; } = "membership_free"; // membership_free | pt_package
    public bool CheckedIn { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReview { get; set; }
    public bool IsReviewed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PtBalanceDto
{
    public int Total { get; set; }
    public int FreeSessions { get; set; }
    public int PackageSessions { get; set; }
    public string? ActivePlanName { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class RespondPtRequest
{
    public bool Accept { get; set; }
    public string? Reason { get; set; }
}

public class CompletePtRequest
{
    public bool Attended { get; set; }
}

public class TrainerPtRequestItemDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? MemberPhone { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string SourceType { get; set; } = "membership_free";
    public bool CheckedIn { get; set; }
    public DateTime CreatedAt { get; set; }
}
