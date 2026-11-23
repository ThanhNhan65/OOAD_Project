using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Services;

/// <summary>
/// Background job định kỳ mỗi 5 phút kiểm tra và đóng danh sách chờ Waitlist tại mốc T-8h trước giờ học.
/// </summary>
public class WaitlistCutoffBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistCutoffBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public WaitlistCutoffBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistCutoffBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IClassBookingService>();
                var expiredCount = await bookingService.ProcessWaitlistCutoffAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    _logger.LogInformation("WaitlistCutoffBackgroundService đã đóng {Count} lượt chờ tại mốc T-8h.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy tác vụ nền kiểm tra mốc đóng Waitlist T-8h.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
