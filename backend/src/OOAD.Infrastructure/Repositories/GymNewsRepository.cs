using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class GymNewsRepository : GenericRepository<GymNews>, IGymNewsRepository
{
    public GymNewsRepository(AppDbContext context) : base(context)
    {
    }
}

