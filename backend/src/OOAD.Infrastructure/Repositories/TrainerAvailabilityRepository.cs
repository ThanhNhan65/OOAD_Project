using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class TrainerAvailabilityRepository : ITrainerAvailabilityRepository
{
    private readonly AppDbContext _context;

    public TrainerAvailabilityRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TrainerAvailability?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TrainerAvailability>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TrainerAvailability entity, CancellationToken cancellationToken = default)
    {
        await _context.TrainerAvailabilities.AddAsync(entity, cancellationToken);
    }

    public void Update(TrainerAvailability entity)
    {
        _context.TrainerAvailabilities.Update(entity);
    }

    public void Delete(TrainerAvailability entity)
    {
        _context.TrainerAvailabilities.Remove(entity);
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

    public async Task<IReadOnlyList<TrainerAvailability>> GetFilteredByTrainerAsync(
        Guid trainerId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TrainerAvailabilities
            .Where(a => a.TrainerId == trainerId)
            .AsNoTracking();

        if (from.HasValue)
            query = query.Where(a => a.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.EndAt <= to.Value);

        return await query
            .OrderBy(a => a.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainerAvailability?> GetByIdWithTrainerDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities
            .Include(a => a.Trainer).ThenInclude(t => t.User)
            .Include(a => a.Trainer).ThenInclude(t => t.Branch)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<TrainerAvailability?> GetByIdAndTrainerIdAsync(
        Guid id,
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TrainerAvailabilities
            .FirstOrDefaultAsync(a => a.Id == id && a.TrainerId == trainerId, cancellationToken);
    }
}
