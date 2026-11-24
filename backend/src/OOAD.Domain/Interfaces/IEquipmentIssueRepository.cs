using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IEquipmentIssueRepository
{
    Task<EquipmentIssue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentIssue>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(EquipmentIssue entity, CancellationToken cancellationToken = default);
    void Update(EquipmentIssue entity);
    void Delete(EquipmentIssue entity);

    Task<IReadOnlyList<EquipmentIssue>> GetReportsWithDetailsAsync(
        Guid? equipmentId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
    Task<EquipmentIssue?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddLogAsync(EquipmentIssueLog log, CancellationToken cancellationToken = default);
    Task<int> CountOpenByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}
