using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface ITrainerAvailabilityRepository
{
    Task<TrainerAvailability?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainerAvailability>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TrainerAvailability entity, CancellationToken cancellationToken = default);
    void Update(TrainerAvailability entity);
    void Delete(TrainerAvailability entity);

    Task<IEnumerable<TrainerAvailability>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainerAvailability>> GetByTrainerAndDateRangeAsync(
        Guid trainerId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);
    Task<bool> HasConflictAsync(
        Guid trainerId,
        DateTime start,
        DateTime end,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainerAvailability>> GetFilteredByTrainerAsync(
        Guid trainerId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
    Task<TrainerAvailability?> GetByIdWithTrainerDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainerAvailability?> GetByIdAndTrainerIdAsync(Guid id, Guid trainerId, CancellationToken cancellationToken = default);
}
