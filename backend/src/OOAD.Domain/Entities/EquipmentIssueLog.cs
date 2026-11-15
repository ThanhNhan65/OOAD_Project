using OOAD.Domain.Common;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Entities;

public class EquipmentIssueLog : BaseEntity
{
    public Guid IssueId { get; set; }
    public Guid StaffId { get; set; }
    public string Note { get; set; } = string.Empty;
    public IssueStatus StatusAfter { get; set; } = IssueStatus.Investigating;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public EquipmentIssue Issue { get; set; } = null!;
    public Staff Staff { get; set; } = null!;
}
