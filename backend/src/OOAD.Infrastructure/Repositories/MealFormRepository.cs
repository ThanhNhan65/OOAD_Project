using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MealFormRepository : GenericRepository<MealForm>, IMealFormRepository
{
    public MealFormRepository(AppDbContext context) : base(context)
    {
    }
}
