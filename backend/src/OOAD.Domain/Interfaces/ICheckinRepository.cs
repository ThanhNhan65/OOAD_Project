using OOAD.Domain.Entities;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Interfaces;

public interface ICheckinRepository
{
    Task<Checkin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Checkin>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Checkin entity, CancellationToken cancellationToken = default);
    void Update(Checkin entity);
    void Delete(Checkin entity);

    Task<Checkin?> GetOpenCheckinByMemberIdAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<Checkin?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Checkin>> GetActiveCheckinsAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Checkin>> GetHistoryAsync(
        Guid? branchId = null,
        Guid? memberId = null,
        CheckinMethod? method = null,
        DateTime? fromDateUtc = null,
        DateTime? toDateUtc = null,
        string? search = null,
        int take = 200,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Checkin>> GetAllOpenCheckinsWithBranchAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, DateTime>> GetCheckedInMemberTimesOnDateAsync(
        Guid branchId,
        DateTime fromUtc,
        DateTime toUtc,
        IEnumerable<Guid> memberIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Checkin>> GetAllByBranchAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
}
