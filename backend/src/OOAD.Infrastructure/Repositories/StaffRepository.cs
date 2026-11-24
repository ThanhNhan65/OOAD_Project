using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly AppDbContext _context;

    public StaffRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Staff?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Staff.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Staff>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Staff.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Staff entity, CancellationToken cancellationToken = default)
    {
        await _context.Staff.AddAsync(entity, cancellationToken);
    }

    public void Update(Staff entity)
    {
        _context.Staff.Update(entity);
    }

    public void Delete(Staff entity)
    {
        _context.Staff.Remove(entity);
    }

    public async Task<Staff?> GetByUserIdWithBranchAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Staff
            .Include(s => s.User)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<Staff?> GetByUserIdWithUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Staff
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetStaffUserIdsByBranchIdAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Staff
            .AsNoTracking()
            .Where(s => s.BranchId == branchId)
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken);
    }
}
