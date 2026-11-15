using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface ITrainerAvailabilityRepository : IGenericRepository<TrainerAvailability>
{
    Task<IEnumerable<TrainerAvailability>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainerAvailability>> GetByTrainerAndDateRangeAsync(Guid trainerId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<bool> HasConflictAsync(Guid trainerId, DateTime start, DateTime end, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
