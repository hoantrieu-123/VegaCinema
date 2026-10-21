using CinemaBooking.Business.Services;
namespace VegaCinema.Infrastructure;
public class SeatExpiryWorker(IServiceScopeFactory scopes, ILogger<SeatExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<SeatHoldService>().ExpireAsync(); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception e) { logger.LogError(e, "Không thể giải phóng ghế hết hạn; sẽ thử lại."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
