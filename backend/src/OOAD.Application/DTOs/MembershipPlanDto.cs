namespace OOAD.Application.DTOs;

public class MembershipPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationMonths => Math.Max(1, (int)Math.Round(DurationDays / 30.0));
    public decimal Price { get; set; }
    public int PriorityLevel { get; set; }
    public int MaxBookingsPerMonth { get; set; }
    public int MaxFreezeTimes { get; set; }
    public int MaxFreezeDays { get; set; }
    public bool IsAllBranches { get; set; }
    public string PlanType { get; set; } = "MEMBERSHIP";
    public int PtSessions { get; set; }
    public List<Guid> BranchIds { get; set; } = new();
    public List<string> BranchNames { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class CreateUpdateMembershipPlanRequest
{
    public string Name { get; set; } = string.Empty;

    public int DurationDays { get; set; } = 30;

    public decimal Price { get; set; }

    public int PriorityLevel { get; set; } = 1;

    public int MaxBookingsPerMonth { get; set; }

    public int MaxFreezeTimes { get; set; }

    public int MaxFreezeDays { get; set; }

    public bool IsAllBranches { get; set; } = true;

    public string PlanType { get; set; } = "MEMBERSHIP";

    public int PtSessions { get; set; }

    public List<Guid>? BranchIds { get; set; }
}
