using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class GymClassRepository : IGymClassRepository
{
    private readonly AppDbContext _context;

    public GymClassRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GymClass?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Classes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<GymClass>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Classes.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(GymClass entity, CancellationToken cancellationToken = default)
    {
        await _context.Classes.AddAsync(entity, cancellationToken);
    }

    public void Update(GymClass entity)
    {
        _context.Classes.Update(entity);
    }

    public void Delete(GymClass entity)
    {
        _context.Classes.Remove(entity);
    }

    private IQueryable<GymClass> QueryWithDetails() => _context.Classes
        .Include(c => c.Branch)
        .Include(c => c.Trainer)
            .ThenInclude(t => t.User)
        .Include(c => c.Sessions)
        .Include(c => c.Reviews);

    public async Task<IReadOnlyList<GymClass>> GetAllWithDetailsAsync(
        Guid? branchId = null,
        Guid? trainerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithDetails().AsNoTracking();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
            query = query.Where(c => c.BranchId == branchId.Value);

        if (trainerId.HasValue && trainerId.Value != Guid.Empty)
            query = query.Where(c => c.TrainerId == trainerId.Value || c.Trainer.UserId == trainerId.Value);

        return await query
            .OrderBy(c => c.DayOfWeek)
            .ThenBy(c => c.StartTime)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GymClass>> GetAllWithBranchAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Include(c => c.Branch)
            .AsQueryable();

        if (branchId.HasValue)
            query = query.Where(c => c.BranchId == branchId.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<GymClass?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithDetails()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> HasRoomConflictAsync(
        Guid branchId,
        string room,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeClassId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoom = room.Trim().ToLower();
        return await _context.Classes
            .AsNoTracking()
            .AnyAsync(c =>
                c.BranchId == branchId &&
                c.Room.Trim().ToLower() == normalizedRoom &&
                c.DayOfWeek == dayOfWeek &&
                (!excludeClassId.HasValue || c.Id != excludeClassId.Value) &&
                c.StartTime < endTime &&
                startTime < c.EndTime,
                cancellationToken);
    }

    public async Task<bool> HasTrainerClassConflictAsync(
        Guid trainerId,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeClassId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.Classes
            .AsNoTracking()
            .AnyAsync(c =>
                c.TrainerId == trainerId &&
                c.DayOfWeek == dayOfWeek &&
                (!excludeClassId.HasValue || c.Id != excludeClassId.Value) &&
                c.StartTime < endTime &&
                startTime < c.EndTime,
                cancellationToken);
    }
}
