using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class StaffRepository : GenericRepository<Staff>, IStaffRepository
{
    public StaffRepository(AppDbContext context) : base(context) {}

    public async Task<Staff?> GetByUserIdWithBranchAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Staff.Include(s => s.User).Include(s => s.Branch)
        .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }
  

}
