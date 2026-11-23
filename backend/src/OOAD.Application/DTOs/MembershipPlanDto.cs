using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "Tên gói tập không được để trống.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 3650, ErrorMessage = "Thời hạn gói (ngày) phải lớn hơn 0.")]
    public int DurationDays { get; set; } = 30;

    [Range(0, double.MaxValue, ErrorMessage = "Giá gói không được âm.")]
    public decimal Price { get; set; }

    [Range(1, 10)]
    public int PriorityLevel { get; set; } = 1;

    [Range(0, 500)]
    public int MaxBookingsPerMonth { get; set; }

    [Range(0, 100)]
    public int MaxFreezeTimes { get; set; }

    [Range(0, 365)]
    public int MaxFreezeDays { get; set; }

    public bool IsAllBranches { get; set; } = true;

    [MaxLength(50)]
    public string PlanType { get; set; } = "MEMBERSHIP";

    [Range(0, 500)]
    public int PtSessions { get; set; }

    public List<Guid>? BranchIds { get; set; }
}
