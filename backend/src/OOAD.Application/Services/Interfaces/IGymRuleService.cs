using OOAD.Application.DTOs.GymRule;

namespace OOAD.Application.Services.Interfaces;

public interface IGymRuleService
{
    Task<IEnumerable<GymRuleDto>> GetAllAsync(Guid? branchId = null, string? category = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<GymRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GymRuleDto> CreateAsync(CreateGymRuleDto dto, Guid? createdBy, CancellationToken cancellationToken = default);
    Task<GymRuleDto?> UpdateAsync(Guid id, UpdateGymRuleDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}