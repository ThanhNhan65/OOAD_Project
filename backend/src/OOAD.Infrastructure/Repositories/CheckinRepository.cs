using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class CheckinRepository : ICheckinRepository
{
    private readonly AppDbContext _context;

    public CheckinRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Checkin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Checkins.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Checkin>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Checkins.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Checkin entity, CancellationToken cancellationToken = default)
    {
        await _context.Checkins.AddAsync(entity, cancellationToken);
    }

    public void Update(Checkin entity)
    {
        _context.Checkins.Update(entity);
    }

    public void Delete(Checkin entity)
    {
        _context.Checkins.Remove(entity);
    }

    private IQueryable<Checkin> QueryWithDetails() => _context.Checkins
        .Include(c => c.Member).ThenInclude(m => m.User)
        .Include(c => c.Member).ThenInclude(m => m.MemberCards)
        .Include(c => c.Branch)
        .Include(c => c.Card)
        .Include(c => c.Recorder).ThenInclude(r => r!.User)
        .Include(c => c.Booking).ThenInclude(b => b!.ClassSession).ThenInclude(cs => cs!.GymClass);

    public async Task<Checkin?> GetOpenCheckinByMemberIdAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithDetails()
            .Where(c => c.MemberId == memberId && c.CheckOutAt == null)
            .OrderByDescending(c => c.CheckInAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Checkin?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithDetails()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Checkin>> GetActiveCheckinsAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithDetails().AsNoTrackingWithIdentityResolution().Where(c => c.CheckOutAt == null);
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        return await query.OrderByDescending(c => c.CheckInAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Checkin>> GetHistoryAsync(
        Guid? branchId = null,
        Guid? memberId = null,
        CheckinMethod? method = null,
        DateTime? fromDateUtc = null,
        DateTime? toDateUtc = null,
        string? search = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithDetails().AsNoTrackingWithIdentityResolution();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
            query = query.Where(c => c.BranchId == branchId.Value);
        if (memberId.HasValue && memberId.Value != Guid.Empty)
            query = query.Where(c => c.MemberId == memberId.Value || c.Member.UserId == memberId.Value);
        if (method.HasValue)
            query = query.Where(c => c.Method == method.Value);
        if (fromDateUtc.HasValue)
            query = query.Where(c => c.CheckInAt >= fromDateUtc.Value);
        if (toDateUtc.HasValue)
            query = query.Where(c => c.CheckInAt < toDateUtc.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Member.User.FullName.ToLower().Contains(term) ||
                c.Member.User.Email.ToLower().Contains(term) ||
                (c.Member.User.Phone != null && c.Member.User.Phone.Contains(term)) ||
                (c.Card != null && c.Card.CardCode.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(c => c.CheckInAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Checkin>> GetAllOpenCheckinsWithBranchAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Checkins
            .Include(c => c.Branch)
            .Where(c => c.CheckOutAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, DateTime>> GetCheckedInMemberTimesOnDateAsync(
        Guid branchId,
        DateTime fromUtc,
        DateTime toUtc,
        IEnumerable<Guid> memberIds,
        CancellationToken cancellationToken = default)
    {
        var idList = memberIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, DateTime>();

        var rows = await _context.Checkins
            .AsNoTracking()
            .Where(c =>
                c.BranchId == branchId &&
                idList.Contains(c.MemberId) &&
                c.CheckInAt >= fromUtc &&
                c.CheckInAt < toUtc)
            .GroupBy(c => c.MemberId)
            .Select(g => new { MemberId = g.Key, FirstCheckInAt = g.Min(x => x.CheckInAt) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.MemberId, x => x.FirstCheckInAt);
    }

    public async Task<IReadOnlyList<Checkin>> GetAllByBranchAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Checkins.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
            query = query.Where(c => c.BranchId == branchId.Value);

        return await query.ToListAsync(cancellationToken);
    }
}
