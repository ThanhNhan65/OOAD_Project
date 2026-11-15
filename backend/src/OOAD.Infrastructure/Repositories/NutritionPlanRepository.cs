using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class NutritionPlanRepository : GenericRepository<NutritionPlan>, INutritionPlanRepository
{
    public NutritionPlanRepository(AppDbContext context) : base(context)
    {
    }
}

