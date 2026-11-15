using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class BodyMeasurementRepository : GenericRepository<BodyMeasurement>, IBodyMeasurementRepository
{
    public BodyMeasurementRepository(AppDbContext context) : base(context)
    {
    }
}

