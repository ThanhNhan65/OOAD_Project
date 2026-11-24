using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class EquipmentIssueRepository : IEquipmentIssueRepository
{
    private readonly AppDbContext _context;

    public EquipmentIssueRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EquipmentIssue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EquipmentIssues.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EquipmentIssue>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EquipmentIssues.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EquipmentIssue entity, CancellationToken cancellationToken = default)
    {
        await _context.EquipmentIssues.AddAsync(entity, cancellationToken);
    }

    public void Update(EquipmentIssue entity)
    {
        _context.EquipmentIssues.Update(entity);
    }

    public void Delete(EquipmentIssue entity)
    {
        _context.EquipmentIssues.Remove(entity);
    }

    public async Task<IReadOnlyList<EquipmentIssue>> GetReportsWithDetailsAsync(
        Guid? equipmentId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EquipmentIssues
            .AsNoTracking()
            .Include(i => i.Equipment)
            .Include(i => i.Logs)
            .AsQueryable();

        if (equipmentId.HasValue)
            query = query.Where(i => i.EquipmentId == equipmentId.Value);

        if (branchId.HasValue)
            query = query.Where(i => i.Equipment.BranchId == branchId.Value);

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentIssue?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EquipmentIssues
            .Include(i => i.Equipment)
            .Include(i => i.Reporter)
            .Include(i => i.Logs)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task AddLogAsync(EquipmentIssueLog log, CancellationToken cancellationToken = default)
    {
        await _context.EquipmentIssueLogs.AddAsync(log, cancellationToken);
    }

    public async Task<int> CountOpenByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.EquipmentIssues
            .AsNoTracking()
            .CountAsync(i =>
                i.Equipment.BranchId == branchId &&
                (i.Status == IssueStatus.Open || i.Status == IssueStatus.Investigating),
                cancellationToken);
    }
}
