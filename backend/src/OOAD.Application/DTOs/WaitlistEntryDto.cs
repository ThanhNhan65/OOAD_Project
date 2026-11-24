namespace OOAD.Application.DTOs;

public class WaitlistEntryDto
{
    public Guid Id { get; set; }
    public Guid ClassSessionId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#7c5cff";
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string TrainerName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public Guid MemberId { get; set; }
    public Guid UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string? PlanName { get; set; }
    public int PriorityLevel { get; set; }
    public int Position { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string Status { get; set; } = "WAITING";
}
