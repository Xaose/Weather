using System.Text.Json.Serialization;

namespace Weather.Models;

public sealed class PushSubscriptionInfo
{
    public string Endpoint { get; set; } = "";
    public string P256Dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationName { get; set; } = "";
    public string Culture { get; set; } = "en";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRainAlertUtc { get; set; }
}

public sealed class PushSubscribeRequest
{
    [JsonPropertyName("subscription")]
    public BrowserPushSubscriptionDto? Subscription { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("locationName")]
    public string? LocationName { get; set; }

    [JsonPropertyName("culture")]
    public string? Culture { get; set; }
}

public sealed class PushUnsubscribeRequest
{
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }
}

public sealed class BrowserPushSubscriptionDto
{
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("keys")]
    public BrowserPushSubscriptionKeysDto? Keys { get; set; }
}

public sealed class BrowserPushSubscriptionKeysDto
{
    [JsonPropertyName("p256dh")]
    public string? P256Dh { get; set; }

    [JsonPropertyName("auth")]
    public string? Auth { get; set; }
}

