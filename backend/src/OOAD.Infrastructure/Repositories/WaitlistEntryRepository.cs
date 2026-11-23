using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class WaitlistEntryRepository : IWaitlistEntryRepository
{
    private readonly AppDbContext _context;

    public WaitlistEntryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.WaitlistEntries.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WaitlistEntry entity, CancellationToken cancellationToken = default)
    {
        await _context.WaitlistEntries.AddAsync(entity, cancellationToken);
    }

    public void Update(WaitlistEntry entity)
    {
        _context.WaitlistEntries.Update(entity);
    }

    public void Delete(WaitlistEntry entity)
    {
        _context.WaitlistEntries.Remove(entity);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetWaitingBySessionOrderedAsync(
        Guid classSessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WaitlistEntries
            .Include(w => w.Member)
                .ThenInclude(m => m.User)
            .Include(w => w.Member)
                .ThenInclude(m => m.Memberships)
                    .ThenInclude(ms => ms.Plan)
            .Where(w => w.ClassSessionId == classSessionId && w.Status == "WAITING")
            .OrderByDescending(w => w.PriorityLevel)
            .ThenBy(w => w.RegisteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WaitlistEntry?> GetActiveEntryAsync(
        Guid classSessionId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WaitlistEntries
            .FirstOrDefaultAsync(
                w => w.ClassSessionId == classSessionId && w.MemberId == memberId && w.Status == "WAITING",
                cancellationToken);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WaitlistEntries
            .AsNoTracking()
            .Include(w => w.Member)
                .ThenInclude(m => m.User)
            .Include(w => w.Member)
                .ThenInclude(m => m.Memberships)
                    .ThenInclude(ms => ms.Plan)
            .Include(w => w.ClassSession)
                .ThenInclude(cs => cs.GymClass)
                    .ThenInclude(gc => gc.Branch)
            .Include(w => w.ClassSession)
                .ThenInclude(cs => cs.Trainer)
                    .ThenInclude(t => t.User)
            .Include(w => w.ClassSession)
                .ThenInclude(cs => cs.WaitlistEntries.Where(x => x.Status == "WAITING"))
            .Where(w => w.MemberId == memberId)
            .OrderByDescending(w => w.RegisteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetWaitingByMemberIdsWithSessionAsync(
        IEnumerable<Guid> memberIds,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var idList = memberIds.Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<WaitlistEntry>();

        var query = _context.WaitlistEntries
            .Include(w => w.ClassSession)
            .Where(w => idList.Contains(w.MemberId) && w.Status == "WAITING");

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.ToListAsync(cancellationToken);
    }
}
