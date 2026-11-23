using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IMembershipRepository
{
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Membership entity, CancellationToken cancellationToken = default);
    void Update(Membership entity);
    void Delete(Membership entity);

    Task<IReadOnlyList<Membership>> GetByMemberIdWithPlanAndFreezeAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> GetByMemberIdFullDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<Membership?> GetByIdAndMemberIdWithDetailsAsync(
        Guid membershipId,
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> GetActiveByMemberIdWithPlanAsync(
        Guid memberId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<Membership?> GetLatestValidGymMembershipAsync(
        Guid memberId,
        DateTime todayVn,
        Guid? excludeMembershipId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> GetAllWithPlanAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
}
