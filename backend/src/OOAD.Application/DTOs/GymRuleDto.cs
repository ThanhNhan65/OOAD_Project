using OOAD.Application.DTOs.GymRule;
namespace OOAD.Application.DTOs.GymRule;

public record GymRuleDto(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid? CreatedBy,
    string Title,
    string Content,
    string Category,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateGymRuleDto(
    Guid? BranchId,
    string Title,
    string Content,
    string Category = "GENERAL"
);

public record UpdateGymRuleDto(
    Guid? BranchId,
    string Title,
    string Content,
    string Category,
    bool IsActive
);