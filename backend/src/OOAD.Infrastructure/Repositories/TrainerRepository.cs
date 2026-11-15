using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace OOAD.Infrastructure.Repositories;

public class TrainerRepository : GenericRepository<Trainer>, ITrainerRepository
{
    public TrainerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Trainer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
    }

    public async Task<Trainer?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .Include(t => t.Availabilities.Where(a => a.StartAt >= DateTime.UtcNow))
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
