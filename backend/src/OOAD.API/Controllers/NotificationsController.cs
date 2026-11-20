using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs.Notification;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetMyNotifications(
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var list = await _notificationService.GetUserNotificationsAsync(userId.Value, unreadOnly, cancellationToken);
        return Ok(list);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(userId.Value, cancellationToken);
        return Ok(new { count });
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var ok = await _notificationService.MarkAsReadAsync(id, userId.Value, cancellationToken);
        if (!ok) return NotFound(new { message = "Không tìm thấy thông báo." });

        return NoContent();
    }

    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _notificationService.DeleteAllAsync(userId.Value, cancellationToken);
        return NoContent();
    }
}