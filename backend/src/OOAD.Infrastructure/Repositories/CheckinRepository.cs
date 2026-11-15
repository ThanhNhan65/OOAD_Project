using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class CheckinRepository : GenericRepository<Checkin>, ICheckinRepository
{
    public CheckinRepository(AppDbContext context) : base(context)
    {
    }
}
