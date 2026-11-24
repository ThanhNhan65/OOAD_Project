using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OOAD.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");

        if (Guid.TryParse(userId, out var parsedId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{parsedId}");
        }

        var roles = Context.User?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToUpperInvariant())
            .Distinct() ?? Enumerable.Empty<string>();

        foreach (var role in roles)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role}");
        }

        await base.OnConnectedAsync();
    }
}
