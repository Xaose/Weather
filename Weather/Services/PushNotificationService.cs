using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Models;
using WebPush;

namespace Weather.Services;

public sealed class PushNotificationService(
    IServiceScopeFactory serviceScopeFactory,
    IPushSubscriptionStore subscriptionStore,
    IOptionsMonitor<PushNotificationOptions> optionsMonitor,
    ILogger<PushNotificationService> logger)
{
    private readonly WebPushClient _webPushClient = new();

    public async Task CheckAndSendRainAlertsAsync(CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.VapidPublicKey)
            || string.IsNullOrWhiteSpace(options.VapidPrivateKey)
            || string.IsNullOrWhiteSpace(options.VapidSubject))
        {
            logger.LogDebug("Push notifications are enabled but VAPID keys are not configured.");
            return;
        }

        var subscriptions = subscriptionStore.GetAll();
        if (subscriptions.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var cooldown = TimeSpan.FromHours(Math.Max(1, options.AlertCooldownHours));
        var threshold = Math.Clamp(options.RainProbabilityThresholdPercent, 1, 100);
        var vapidDetails = new VapidDetails(options.VapidSubject, options.VapidPublicKey, options.VapidPrivateKey);

        using var scope = serviceScopeFactory.CreateScope();
        var weatherService = scope.ServiceProvider.GetRequiredService<WeatherService>();

        foreach (var subscription in subscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (subscription.LastRainAlertUtc.HasValue && now - subscription.LastRainAlertUtc.Value < cooldown)
            {
                continue;
            }

            try
            {
                var forecast = await weatherService.GetDailyForecastAsync(subscription.Latitude, subscription.Longitude, 1, cancellationToken);
                if (forecast is null || !HasRainWithin24Hours(forecast, threshold))
                {
                    continue;
                }

                var payloadJson = BuildPayload(subscription.Culture, subscription.LocationName);
                var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256Dh, subscription.Auth);

                await _webPushClient.SendNotificationAsync(pushSubscription, payloadJson, vapidDetails, cancellationToken: cancellationToken);
                subscriptionStore.MarkRainAlertSent(subscription.Endpoint, now);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                subscriptionStore.Remove(subscription.Endpoint);
                logger.LogInformation("Removed expired push subscription {Endpoint}.", subscription.Endpoint);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process push notification for endpoint {Endpoint}.", subscription.Endpoint);
            }
        }
    }

    private static bool HasRainWithin24Hours(HourlyForecastDto forecast, int thresholdPercent)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var endUtc = nowUtc.AddHours(24);

        return forecast.HourlyForecasts.Any(entry =>
        {
            var timeUtc = DateTime.SpecifyKind(entry.TimeUts, DateTimeKind.Utc);
            if (timeUtc < nowUtc.UtcDateTime || timeUtc > endUtc.UtcDateTime)
            {
                return false;
            }

            if (entry.PrecipitationProbabilityPercent < thresholdPercent)
            {
                return false;
            }

            var condition = (entry.Condition ?? string.Empty).ToLowerInvariant();
            var description = (entry.Description ?? string.Empty).ToLowerInvariant();
            return condition.Contains("rain", StringComparison.Ordinal)
                || condition.Contains("drizzle", StringComparison.Ordinal)
                || condition.Contains("thunderstorm", StringComparison.Ordinal)
                || description.Contains("rain", StringComparison.Ordinal)
                || description.Contains("shower", StringComparison.Ordinal);
        });
    }

    private static string BuildPayload(string culture, string locationName)
    {
        var normalizedCulture = (culture ?? string.Empty).Trim().ToLowerInvariant();
        var cleanLocation = string.IsNullOrWhiteSpace(locationName) ? string.Empty : locationName.Trim();

        var body = normalizedCulture switch
        {
            "ru" => string.IsNullOrWhiteSpace(cleanLocation)
                ? "В ближайшие 24 часа ожидается дождь."
                : $"В {cleanLocation} в ближайшие 24 часа ожидается дождь.",
            "be" => string.IsNullOrWhiteSpace(cleanLocation)
                ? "У бліжэйшыя 24 гадзіны чакаецца дождж."
                : $"У {cleanLocation} у бліжэйшыя 24 гадзіны чакаецца дождж.",
            _ => string.IsNullOrWhiteSpace(cleanLocation)
                ? "Rain is expected within the next 24 hours."
                : $"Rain is expected in {cleanLocation} within the next 24 hours."
        };

        var title = normalizedCulture switch
        {
            "ru" => "Погодное предупреждение",
            "be" => "Папярэджанне надвор'я",
            _ => "Weather alert"
        };

        return JsonSerializer.Serialize(new
        {
            title,
            body,
            url = "/"
        });
    }
}
