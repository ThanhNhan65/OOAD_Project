using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MetricTypeRepository : GenericRepository<MetricType>, IMetricTypeRepository
{
    public MetricTypeRepository(AppDbContext context) : base(context)
    {
    }
}

