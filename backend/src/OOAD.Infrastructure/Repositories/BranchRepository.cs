using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _context;

    public BranchRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Branch entity, CancellationToken cancellationToken = default)
    {
        await _context.Branches.AddAsync(entity, cancellationToken);
    }

    public void Update(Branch entity)
    {
        _context.Branches.Update(entity);
    }

    public void Delete(Branch entity)
    {
        _context.Branches.Remove(entity);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Branches.AnyAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetExistingIdsAsync(
        IEnumerable<Guid> branchIds,
        CancellationToken cancellationToken = default)
    {
        var idList = branchIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<Guid>();

        return await _context.Branches
            .AsNoTracking()
            .Where(b => idList.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Branch?> GetFirstOrderedByNameAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
