using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface ISystemSettingService
{
    Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<SystemSettingDto?> GetByKeyAsync(string key, Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<AppSettingsSummaryDto> GetSummaryAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<AppSettingsSummaryDto> UpdateSummaryAsync(UpdateAppSettingsSummaryRequest request, Guid? updaterId = null, Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<SystemSettingDto> UpsertSettingAsync(UpdateSystemSettingRequest request, Guid? updaterId = null, CancellationToken cancellationToken = default);
}
