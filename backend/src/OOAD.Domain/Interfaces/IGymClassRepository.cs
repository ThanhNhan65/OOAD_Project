using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IGymClassRepository : IGenericRepository<GymClass>
{
    Task<IReadOnlyList<GymClass>> GetAllWithDetailsAsync(
        Guid? branchId = null,
        Guid? trainerId = null,
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
