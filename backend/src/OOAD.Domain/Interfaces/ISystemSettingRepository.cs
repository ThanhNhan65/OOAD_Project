using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface ISystemSettingRepository : IGenericRepository<SystemSetting>
{
    Task<int> GetIntSettingAsync(
        string key,
        int defaultValue,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
}

