using System.ComponentModel.DataAnnotations;

namespace OOAD.Application.DTOs;

public class MembershipDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanType { get; set; } = "MEMBERSHIP";
    public decimal Price { get; set; }
    public int PriorityLevel { get; set; }
    public int DurationDays { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsAllBranches { get; set; }
    public List<Guid> BranchIds { get; set; } = new();
    public Guid? RenewedFromId { get; set; }
    public bool IsRenewal { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int RemainingSessions { get; set; }
    public int TotalPtSessions { get; set; }
    public int MaxFreezeTimes { get; set; }
    public int MaxFreezeDays { get; set; }
    public int FreezeCountUsed { get; set; }
    public int FreezeDaysUsed { get; set; }
    public string Status { get; set; } = "Active";
    public string PayStatus { get; set; } = "paid";
    public DateTime CreatedAt { get; set; }
}

public class PurchaseMembershipRequest
{
    [Required(ErrorMessage = "Vui lòng chọn gói tập cần đăng ký.")]
    public Guid PlanId { get; set; }

    public Guid? BranchId { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "ZaloPay";

    public bool SimulateSuccess { get; set; } = true;
}

public class RetryPaymentRequest
{
    public Guid? MembershipId { get; set; }

    public Guid? BranchId { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "ZaloPay";

    public bool SimulateSuccess { get; set; } = true;
}
