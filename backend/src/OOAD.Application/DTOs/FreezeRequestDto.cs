namespace OOAD.Application.DTOs;

public class FreezeRequestDto
{
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? MemberPhone { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime PlanEndDate { get; set; }
    public int MaxFreezeTimes { get; set; }
    public int MaxFreezeDays { get; set; }
    public int FreezeCountUsed { get; set; }
    public int FreezeDaysUsed { get; set; }
    public int AffectedBookingsCount { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Days { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public Guid? HandledBy { get; set; }
    public string? HandledByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
}

public class CreateFreezeRequest
{
    public Guid MembershipId { get; set; }

    public DateTime FromDate { get; set; }

    public int Days { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public class HandleFreezeRequest
{
    public bool Approve { get; set; }

    public string? Note { get; set; }
}
