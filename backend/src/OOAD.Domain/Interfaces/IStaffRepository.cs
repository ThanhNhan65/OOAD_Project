using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Staff>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Staff entity, CancellationToken cancellationToken = default);
    void Update(Staff entity);
    void Delete(Staff entity);

    Task<Staff?> GetByUserIdWithBranchAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Staff?> GetByUserIdWithUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetStaffUserIdsByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);
}
