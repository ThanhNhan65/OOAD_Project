using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class WorkoutMeasurementRepository : GenericRepository<WorkoutMeasurement>, IWorkoutMeasurementRepository
{
    public WorkoutMeasurementRepository(AppDbContext context) : base(context)
    {
    }
}
