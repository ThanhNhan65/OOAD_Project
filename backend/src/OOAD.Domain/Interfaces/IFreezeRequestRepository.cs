using OOAD.Domain.Entities;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Interfaces;

public interface IFreezeRequestRepository
{
    Task<FreezeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FreezeRequest>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(FreezeRequest entity, CancellationToken cancellationToken = default);
    void Update(FreezeRequest entity);
    void Delete(FreezeRequest entity);

    Task<IReadOnlyList<FreezeRequest>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FreezeRequest>> GetAllWithDetailsAsync(
        FreezeRequestStatus? status = null,
        CancellationToken cancellationToken = default);
    Task<FreezeRequest?> GetByIdWithFullDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
}
