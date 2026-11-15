using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface ITrainerRepository : IGenericRepository<Trainer>
{
    Task<Trainer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Trainer?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Trainer>> GetAllWithDetailsAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
}
