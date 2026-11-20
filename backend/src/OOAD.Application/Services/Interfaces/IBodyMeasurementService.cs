using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IBodyMeasurementService
{
    Task<IEnumerable<BodyMeasurementDto>> GetMemberMeasurementsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BodyMeasurementDto> CreateMeasurementAsync(Guid userId, CreateBodyMeasurementRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<MetricTypeDto>> GetMetricTypesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<NutritionPlanDto>> GetNutritionPlansAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NutritionPlanDto> CreateNutritionPlanAsync(Guid trainerUserId, CreateNutritionPlanRequest request, CancellationToken cancellationToken = default);
}
