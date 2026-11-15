using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class MembershipPlanRepository : GenericRepository<MembershipPlan>, IMembershipPlanRepository
{
    public MembershipPlanRepository(AppDbContext context) : base(context)
    {
    }
}
