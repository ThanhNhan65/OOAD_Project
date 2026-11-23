using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class SystemSettingRepository : GenericRepository<SystemSetting>, ISystemSettingRepository
{
    public SystemSettingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<int> GetIntSettingAsync(
        string key,
        int defaultValue,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = key.Trim();
        var candidates = await _context.SystemSettings
            .AsNoTracking()
            .Where(s => s.SettingKey == normalizedKey)
            .ToListAsync(cancellationToken);

        var matched = (branchId.HasValue
                ? candidates.FirstOrDefault(s => s.BranchId == branchId.Value)
                : null)
            ?? candidates.FirstOrDefault(s => s.BranchId == null)
            ?? candidates.FirstOrDefault();

        if (matched != null && int.TryParse(matched.SettingValue, out var parsed) && parsed >= 0)
            return parsed;

        return defaultValue;
    }
}

