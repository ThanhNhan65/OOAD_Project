using Microsoft.AspNetCore.SignalR;
using OOAD.API.Hubs;
using OOAD.Application.Common.Interfaces;

namespace OOAD.API.Services;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRRealtimeNotifier> _logger;

    public SignalRRealtimeNotifier(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyUsersAsync(
        IEnumerable<Guid> userIds,
        string? latestTitle = null,
        string? latestContent = null,
        CancellationToken ct = default)
    {
        var distinctIds = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0) return;

        try
        {
            var payload = string.IsNullOrWhiteSpace(latestTitle)
                ? null
                : new { title = latestTitle, content = latestContent };

            foreach (var uid in distinctIds)
            {
                var group = $"user_{uid}";
                await _hubContext.Clients.Group(group).SendAsync("NotificationsUpdated", payload, ct);
                await _hubContext.Clients.Group(group).SendAsync("NavCountsUpdated", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đẩy thông báo SignalR tới người dùng.");
        }
    }

    public async Task NotifyRolesAsync(IEnumerable<string> roles, CancellationToken ct = default)
    {
        var distinctRoles = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (distinctRoles.Count == 0) return;

        try
        {
            foreach (var role in distinctRoles)
            {
                await _hubContext.Clients.Group($"role_{role}").SendAsync("NavCountsUpdated", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đẩy cập nhật badge SignalR tới nhóm vai trò.");
        }
    }
}
