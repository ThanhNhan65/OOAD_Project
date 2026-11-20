using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IWaitlistEntryRepository : IGenericRepository<WaitlistEntry>
{
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
}
