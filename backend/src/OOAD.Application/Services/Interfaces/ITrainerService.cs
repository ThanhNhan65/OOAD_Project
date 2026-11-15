using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface ITrainerService
{
    Task<IEnumerable<TrainerDto>> GetAllTrainersAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<TrainerDto?> GetTrainerByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainerDto?> GetCurrentTrainerProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TrainerDto> UpdateTrainerProfileAsync(Guid userId, UpdateTrainerProfileRequest request, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<TrainerAvailabilityDto>> GetTrainerAvailabilityAsync(Guid trainerId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<TrainerAvailabilityDto> AddAvailabilityAsync(Guid userId, AvailabilityRequest request, CancellationToken cancellationToken = default);
    Task DeleteAvailabilityAsync(Guid userId, Guid availabilityId, CancellationToken cancellationToken = default);

    Task<IEnumerable<TrainerScheduleItemDto>> GetMyScheduleAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}
