using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MembershipPlanRepository : IMembershipPlanRepository
{
    private readonly AppDbContext _context;

    public MembershipPlanRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MembershipPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MembershipPlans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipPlan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MembershipPlans.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MembershipPlan entity, CancellationToken cancellationToken = default)
    {
        await _context.MembershipPlans.AddAsync(entity, cancellationToken);
    }

    public void Update(MembershipPlan entity)
    {
        _context.MembershipPlans.Update(entity);
    }

    public void Delete(MembershipPlan entity)
    {
        _context.MembershipPlans.Remove(entity);
    }

    public async Task<IReadOnlyList<MembershipPlan>> GetAllWithBranchesAsync(
        string? planType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MembershipPlans
            .AsNoTracking()
            .Include(p => p.PlanBranches)
                .ThenInclude(pb => pb.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(planType) && !planType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedType = planType.Trim().ToUpperInvariant();
            query = query.Where(p => p.PlanType.ToUpper() == normalizedType);
        }

        return await query
            .OrderBy(p => p.PlanType)
            .ThenBy(p => p.PriorityLevel)
            .ThenBy(p => p.Price)
            .ToListAsync(cancellationToken);
    }

    public async Task<MembershipPlan?> GetByIdWithBranchesAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MembershipPlans
            .Include(p => p.PlanBranches)
                .ThenInclude(pb => pb.Branch)
            .AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<MembershipPlan?> GetByIdWithMembershipsAndBranchesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.MembershipPlans
            .Include(p => p.Memberships)
            .Include(p => p.PlanBranches)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddPlanBranchAsync(PlanBranch planBranch, CancellationToken cancellationToken = default)
    {
        await _context.PlanBranches.AddAsync(planBranch, cancellationToken);
    }

    public void RemovePlanBranches(IEnumerable<PlanBranch> planBranches)
    {
        _context.PlanBranches.RemoveRange(planBranches);
    }
}
