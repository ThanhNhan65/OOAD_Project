using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IBodyMeasurementService
{
    Task<IEnumerable<BodyMeasurementDto>> GetMemberMeasurementsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BodyMeasurementDto> CreateMeasurementAsync(Guid userId, CreateBodyMeasurementRequest request, CancellationToken cancellationToken = default);
    Task DeleteMeasurementAsync(Guid userId, Guid measurementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MetricTypeDto>> GetMetricTypesAsync(CancellationToken cancellationToken = default);
}
