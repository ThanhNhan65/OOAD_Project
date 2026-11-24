using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class FreezeRequestRepository : IFreezeRequestRepository
{
    private readonly AppDbContext _context;

    public FreezeRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FreezeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FreezeRequests.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<FreezeRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FreezeRequests.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FreezeRequest entity, CancellationToken cancellationToken = default)
    {
        await _context.FreezeRequests.AddAsync(entity, cancellationToken);
    }

    public void Update(FreezeRequest entity)
    {
        _context.FreezeRequests.Update(entity);
    }

    public void Delete(FreezeRequest entity)
    {
        _context.FreezeRequests.Remove(entity);
    }

    public async Task<IReadOnlyList<FreezeRequest>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.FreezeRequests
            .AsNoTrackingWithIdentityResolution()
            .Include(f => f.Membership).ThenInclude(m => m.Plan)
            .Include(f => f.Membership).ThenInclude(m => m.FreezeRequests)
            .Include(f => f.Handler).ThenInclude(s => s!.User)
            .Where(f => f.Membership.MemberId == memberId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FreezeRequest>> GetAllWithDetailsAsync(
        FreezeRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.FreezeRequests
            .AsNoTrackingWithIdentityResolution()
            .Include(f => f.Membership).ThenInclude(m => m.Plan)
            .Include(f => f.Membership).ThenInclude(m => m.Member).ThenInclude(mb => mb.User)
            .Include(f => f.Membership).ThenInclude(m => m.FreezeRequests)
            .Include(f => f.Handler).ThenInclude(s => s!.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(f => f.Status == status.Value);

        return await query
            .OrderBy(f => f.Status == FreezeRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<FreezeRequest?> GetByIdWithFullDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.FreezeRequests
            .Include(f => f.Membership).ThenInclude(m => m.Plan)
            .Include(f => f.Membership).ThenInclude(m => m.Member).ThenInclude(mb => mb.User)
            .Include(f => f.Membership).ThenInclude(m => m.FreezeRequests)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FreezeRequests
            .AsNoTracking()
            .CountAsync(f => f.Status == FreezeRequestStatus.Pending, cancellationToken);
    }
}
