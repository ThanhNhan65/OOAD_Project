using OOAD.Application.DTOs.Notification;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(INotificationRepository notificationRepository, IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var list = await _notificationRepository.GetByUserIdAsync(userId, unreadOnly, cancellationToken);
        return list.Select(n => new NotificationDto(
            n.Id,
            n.UserId,
            n.Type,
            n.Title,
            n.Content,
            n.IsRead,
            n.CreatedAt,
            n.RefType,
            n.RefId
        ));
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _notificationRepository.CountUnreadByUserIdAsync(userId, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var item = await _notificationRepository.GetByIdAndUserIdAsync(id, userId, cancellationToken);
        if (item == null) return false;

        if (!item.IsRead)
        {
            item.IsRead = true;
            _notificationRepository.Update(item);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadList = await _notificationRepository.GetUnreadByUserIdTrackedAsync(userId, cancellationToken);
        if (unreadList.Count == 0) return;

        foreach (var n in unreadList)
        {
            n.IsRead = true;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var list = await _notificationRepository.GetAllByUserIdTrackedAsync(userId, cancellationToken);
        if (list.Count == 0) return;

        _notificationRepository.DeleteRange(list);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
