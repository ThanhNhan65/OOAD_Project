using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IWaitlistEntryRepository
{
    Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitlistEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WaitlistEntry entity, CancellationToken cancellationToken = default);
    void Update(WaitlistEntry entity);
    void Delete(WaitlistEntry entity);

    Task<IReadOnlyList<WaitlistEntry>> GetWaitingBySessionOrderedAsync(
        Guid classSessionId,
        CancellationToken cancellationToken = default);
    Task<WaitlistEntry?> GetActiveEntryAsync(
        Guid classSessionId,
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitlistEntry>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitlistEntry>> GetWaitingByMemberIdsWithSessionAsync(
        IEnumerable<Guid> memberIds,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);
}
