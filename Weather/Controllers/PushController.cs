using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Weather.Models;
using Weather.Services;

namespace Weather.Controllers;

[ApiController]
[Route("api/push")]
public sealed class PushController(
    IPushSubscriptionStore subscriptionStore,
    IOptionsMonitor<PushNotificationOptions> optionsMonitor) : ControllerBase
{
    [HttpGet("public-key")]
    public IActionResult PublicKey()
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push notifications are disabled." });
        }

        if (string.IsNullOrWhiteSpace(options.VapidPublicKey))
        {
            return NotFound(new { error = "VAPID public key is not configured." });
        }

        return Ok(new { publicKey = options.VapidPublicKey });
    }

    [HttpPost("subscribe")]
    public IActionResult Subscribe([FromBody] PushSubscribeRequest request)
    {
        if (request.Subscription is null
            || string.IsNullOrWhiteSpace(request.Subscription.Endpoint)
            || string.IsNullOrWhiteSpace(request.Subscription.Keys?.P256Dh)
            || string.IsNullOrWhiteSpace(request.Subscription.Keys.Auth))
        {
            return BadRequest(new { error = "Invalid push subscription payload." });
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return BadRequest(new { error = "Invalid coordinates." });
        }

        var culture = string.IsNullOrWhiteSpace(request.Culture)
            ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            : request.Culture.Trim().ToLowerInvariant();

        var subscription = new PushSubscriptionInfo
        {
            Endpoint = request.Subscription.Endpoint.Trim(),
            P256Dh = request.Subscription.Keys.P256Dh.Trim(),
            Auth = request.Subscription.Keys.Auth.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationName = request.LocationName?.Trim() ?? string.Empty,
            Culture = culture,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        subscriptionStore.Upsert(subscription);
        return Ok(new { success = true });
    }

    [HttpPost("unsubscribe")]
    public IActionResult Unsubscribe([FromBody] PushUnsubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new { error = "Endpoint is required." });
        }

        var removed = subscriptionStore.Remove(request.Endpoint.Trim());
        return Ok(new { success = true, removed });
    }
}

