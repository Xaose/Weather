using Microsoft.Extensions.Options;
using Weather.Models;

namespace Weather.Services;

public sealed class PushWeatherCheckWorker(
    PushNotificationService pushNotificationService,
    IOptionsMonitor<PushNotificationOptions> optionsMonitor,
    ILogger<PushWeatherCheckWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pushNotificationService.CheckAndSendRainAlertsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unexpected error in push weather check worker.");
            }

            var minutes = Math.Max(5, optionsMonitor.CurrentValue.CheckIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }
}

