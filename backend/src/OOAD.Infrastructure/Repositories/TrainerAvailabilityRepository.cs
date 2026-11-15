using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace OOAD.Infrastructure.Repositories;

public class TrainerAvailabilityRepository : GenericRepository<TrainerAvailability>, ITrainerAvailabilityRepository
{
    public TrainerAvailabilityRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TrainerAvailability>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities
            .Where(a => a.TrainerId == trainerId)
            .OrderBy(a => a.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TrainerAvailability>> GetByTrainerAndDateRangeAsync(Guid trainerId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities
            .Where(a => a.TrainerId == trainerId && a.StartAt >= start && a.EndAt <= end)
            .OrderBy(a => a.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasConflictAsync(Guid trainerId, DateTime start, DateTime end, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities
            .AnyAsync(a => a.TrainerId == trainerId
                        && (!excludeId.HasValue || a.Id != excludeId.Value)
                        && a.StartAt < end
                        && a.EndAt > start,
                      cancellationToken);
    }
}
