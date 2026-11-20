using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MembershipRepository : GenericRepository<Membership>, IMembershipRepository
{
    public MembershipRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Membership>> GetByMemberIdWithPlanAndFreezeAsync(
        Guid memberId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Memberships
            .AsNoTracking()
            .Include(m => m.Branch)
                .ThenInclude(p => p.PlanBranches)
            .Include(m => m.FreezeRequests)
            .Where(m => m.MemberId == memberId)
            .OrderByDescending(m => m.EndDate)
            .ToListAsync(cancellationToken);
    }
}
