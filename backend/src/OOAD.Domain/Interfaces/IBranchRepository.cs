using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Branch entity, CancellationToken cancellationToken = default);
    void Update(Branch entity);
    void Delete(Branch entity);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetExistingIdsAsync(IEnumerable<Guid> branchIds, CancellationToken cancellationToken = default);
    Task<Branch?> GetFirstOrderedByNameAsync(CancellationToken cancellationToken = default);
}
