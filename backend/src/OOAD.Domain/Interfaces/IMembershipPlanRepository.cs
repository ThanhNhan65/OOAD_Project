using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IMembershipPlanRepository
{
    Task<MembershipPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MembershipPlan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MembershipPlan entity, CancellationToken cancellationToken = default);
    void Update(MembershipPlan entity);
    void Delete(MembershipPlan entity);

    Task<IReadOnlyList<MembershipPlan>> GetAllWithBranchesAsync(
        string? planType = null,
        CancellationToken cancellationToken = default);
    Task<MembershipPlan?> GetByIdWithBranchesAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);
    Task<MembershipPlan?> GetByIdWithMembershipsAndBranchesAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task AddPlanBranchAsync(PlanBranch planBranch, CancellationToken cancellationToken = default);
    void RemovePlanBranches(IEnumerable<PlanBranch> planBranches);
}
