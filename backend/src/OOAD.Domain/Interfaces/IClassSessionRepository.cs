using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IClassSessionRepository
{
    Task<ClassSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSession>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ClassSession entity, CancellationToken cancellationToken = default);
    void Update(ClassSession entity);
    void Delete(ClassSession entity);

    Task<IReadOnlyList<ClassSession>> GetSessionsWithDetailsAsync(
        Guid? branchId = null,
        Guid? classId = null,
        Guid? trainerId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? status = null,
        CancellationToken cancellationToken = default);
    Task<ClassSession?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<bool> HasRoomConflictAsync(
        Guid branchId,
        string room,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default);
    Task<bool> HasTrainerSessionConflictAsync(
        Guid trainerId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSession>> GetSessionsPastWaitlistCutoffWithWaitingEntriesAsync(
        DateTime cutoffThresholdUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSession>> GetByTrainerAndRangeAsync(
        Guid trainerId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSession>> GetPastNonCancelledWithDetailsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
