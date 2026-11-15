using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MeasurementValueRepository : GenericRepository<MeasurementValue>, IMeasurementValueRepository
{
    public MeasurementValueRepository(AppDbContext context) : base(context)
    {
    }
}

