using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IGymClassRepository
{
    Task<GymClass?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GymClass>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(GymClass entity, CancellationToken cancellationToken = default);
    void Update(GymClass entity);
    void Delete(GymClass entity);

    Task<IReadOnlyList<GymClass>> GetAllWithDetailsAsync(
        Guid? branchId = null,
        Guid? trainerId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GymClass>> GetAllWithBranchAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
    Task<GymClass?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<bool> HasRoomConflictAsync(
        Guid branchId,
        string room,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeClassId = null,
        CancellationToken cancellationToken = default);
    Task<bool> HasTrainerClassConflictAsync(
        Guid trainerId,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeClassId = null,
        CancellationToken cancellationToken = default);
}
