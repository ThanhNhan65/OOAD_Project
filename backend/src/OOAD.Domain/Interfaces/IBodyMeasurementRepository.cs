using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IBodyMeasurementRepository
{
    Task<BodyMeasurement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BodyMeasurement>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(BodyMeasurement entity, CancellationToken cancellationToken = default);
    void Update(BodyMeasurement entity);
    void Delete(BodyMeasurement entity);

    Task<IReadOnlyList<BodyMeasurement>> GetByMemberIdWithValuesAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<BodyMeasurement?> GetByIdWithValuesAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<BodyMeasurement?> GetByIdAndMemberIdAsync(
        Guid measurementId,
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<bool> HasAnyForMemberAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
}
