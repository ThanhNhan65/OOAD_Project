using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IMembershipRepository : IGenericRepository<Membership>
{
    Task<IReadOnlyList<Membership>> GetByMemberIdWithPlanAndFreezeAsync(
        Guid memberId, 
        CancellationToken cancellationToken = default);

}
