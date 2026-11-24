using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Payment entity, CancellationToken cancellationToken = default);
    void Update(Payment entity);
    void Delete(Payment entity);

    Task<IReadOnlyList<Payment>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetAllWithDetailsAsync(
        Guid? branchId = null,
        int take = 300,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetCompletedWithDetailsAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default);
}
