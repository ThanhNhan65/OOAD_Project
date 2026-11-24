using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Equipment entity, CancellationToken cancellationToken = default);
    void Update(Equipment entity);
    void Delete(Equipment entity);

    Task<IReadOnlyList<Equipment>> GetAllByBranchAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
}
