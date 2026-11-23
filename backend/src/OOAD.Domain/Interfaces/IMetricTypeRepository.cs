using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IMetricTypeRepository
{
    Task<MetricType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetricType>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MetricType entity, CancellationToken cancellationToken = default);
    void Update(MetricType entity);
    void Delete(MetricType entity);

    Task<IReadOnlyList<MetricType>> GetActiveOrderedAsync(CancellationToken cancellationToken = default);
}
