namespace OOAD.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyUsersAsync(
        IEnumerable<Guid> userIds,
        string? latestTitle = null,
        string? latestContent = null,
        CancellationToken ct = default);

    Task NotifyRolesAsync(
        IEnumerable<string> roles,
        CancellationToken ct = default);
}
