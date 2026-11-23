using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MembershipRepository : IMembershipRepository
{
    private readonly AppDbContext _context;

    public MembershipRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Membership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Memberships.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Membership>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Memberships.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Membership entity, CancellationToken cancellationToken = default)
    {
        await _context.Memberships.AddAsync(entity, cancellationToken);
    }

    public void Update(Membership entity)
    {
        _context.Memberships.Update(entity);
    }

    public void Delete(Membership entity)
    {
        _context.Memberships.Remove(entity);
    }

    public async Task<IReadOnlyList<Membership>> GetByMemberIdWithPlanAndFreezeAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Branch)
            .Include(m => m.Plan)
                .ThenInclude(p => p.PlanBranches)
            .Include(m => m.FreezeRequests)
            .Where(m => m.MemberId == memberId)
            .OrderByDescending(m => m.EndDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Membership>> GetByMemberIdFullDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Plan).ThenInclude(p => p.PlanBranches)
            .Include(m => m.Branch)
            .Include(m => m.FreezeRequests)
            .Include(m => m.Payments)
            .Where(m => m.MemberId == memberId)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Membership?> GetByIdAndMemberIdWithDetailsAsync(
        Guid membershipId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Memberships
            .Include(m => m.Plan).ThenInclude(p => p.PlanBranches).ThenInclude(pb => pb.Branch)
            .Include(m => m.Branch)
            .Include(m => m.FreezeRequests)
            .Include(m => m.Payments)
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.MemberId == memberId, cancellationToken);
    }

    public async Task<IReadOnlyList<Membership>> GetActiveByMemberIdWithPlanAsync(
        Guid memberId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.Memberships
            .Include(m => m.Plan)
            .Where(m => m.MemberId == memberId && m.Status == MembershipStatus.Active && m.EndDate >= nowUtc)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Membership?> GetLatestValidGymMembershipAsync(
        Guid memberId,
        DateTime todayVn,
        Guid? excludeMembershipId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Memberships
            .Include(m => m.Plan)
            .Where(m => m.MemberId == memberId &&
                        m.Plan.PlanType.ToUpper() == "MEMBERSHIP" &&
                        (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Frozen) &&
                        m.EndDate.Date >= todayVn);

        if (excludeMembershipId.HasValue)
            query = query.Where(m => m.Id != excludeMembershipId.Value);

        return await query
            .OrderByDescending(m => m.EndDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Membership>> GetAllWithPlanAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Memberships
            .AsNoTracking()
            .Include(m => m.Plan)
            .AsQueryable();

        if (branchId.HasValue)
            query = query.Where(m => m.BranchId == branchId.Value);

        return await query.ToListAsync(cancellationToken);
    }
}
