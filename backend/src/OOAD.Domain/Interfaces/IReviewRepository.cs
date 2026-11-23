using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Review entity, CancellationToken cancellationToken = default);
    void Update(Review entity);
    void Delete(Review entity);

    Task<IReadOnlyList<Review>> GetByTrainerIdWithDetailsAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default);
}
