namespace OOAD.Application.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanType { get; set; } = "MEMBERSHIP";
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public string? GatewayTxnCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
