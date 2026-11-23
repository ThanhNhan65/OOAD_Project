using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Notification entity, CancellationToken cancellationToken = default);
    void Update(Notification entity);
    void Delete(Notification entity);

    Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);
    Task<int> CountUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetUnreadByUserIdTrackedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllByUserIdTrackedAsync(Guid userId, CancellationToken cancellationToken = default);
    void DeleteRange(IEnumerable<Notification> notifications);
}
