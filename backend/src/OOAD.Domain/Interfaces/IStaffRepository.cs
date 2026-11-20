using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IStaffRepository : IGenericRepository<Staff>
{
    Task<Staff?> GetByUserIdWithBranchAsync(Guid userId, CancellationToken cancellationToken = default);
}
