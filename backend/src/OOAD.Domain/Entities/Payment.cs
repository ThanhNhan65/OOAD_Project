using OOAD.Domain.Common;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid MembershipId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "ZaloPay";
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    public string? GatewayTxnCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    // Navigation properties
    public Membership Membership { get; set; } = null!;
    public string? IdempotencyKey {get; set;}
    public string? FailureReason {get; set;}
}
