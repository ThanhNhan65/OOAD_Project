namespace OOAD.Application.DTOs.Notification;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Content,
    bool IsRead,
    DateTime CreatedAt,
    string? RefType,
    Guid? RefId
);