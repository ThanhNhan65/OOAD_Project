using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface ITrainerRepository
{
    Task<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trainer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Trainer entity, CancellationToken cancellationToken = default);
    void Update(Trainer entity);
    void Delete(Trainer entity);

    Task<Trainer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Trainer?> GetByUserIdWithDetailsAsync(
        Guid userId,
        bool includeAvailabilities = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);
    Task<Trainer?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Trainer>> GetAllWithDetailsAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
}
