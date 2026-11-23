using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class TrainerRepository : ITrainerRepository
{
    private readonly AppDbContext _context;

    public TrainerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Trainers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Trainer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Trainers.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Trainer entity, CancellationToken cancellationToken = default)
    {
        await _context.Trainers.AddAsync(entity, cancellationToken);
    }

    public void Update(Trainer entity)
    {
        _context.Trainers.Update(entity);
    }

    public void Delete(Trainer entity)
    {
        _context.Trainers.Remove(entity);
    }

    public async Task<Trainer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
    }

    public async Task<Trainer?> GetByUserIdWithDetailsAsync(
        Guid userId,
        bool includeAvailabilities = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .AsQueryable();

        if (includeAvailabilities)
            query = query.Include(t => t.Availabilities.OrderBy(a => a.StartAt));

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
    }

    public async Task<Trainer?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .Include(t => t.Availabilities.Where(a => a.StartAt >= DateTime.UtcNow && !a.IsBooked))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Trainer>> GetAllWithDetailsAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(t => t.BranchId == branchId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
