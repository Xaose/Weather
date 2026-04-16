using System.Collections.Concurrent;
using Weather.Models;

namespace Weather.Services;

public sealed class InMemoryPushSubscriptionStore : IPushSubscriptionStore
{
    private readonly ConcurrentDictionary<string, PushSubscriptionInfo> _subscriptions = new(StringComparer.Ordinal);

    public IReadOnlyCollection<PushSubscriptionInfo> GetAll() => _subscriptions.Values.ToArray();

    public void Upsert(PushSubscriptionInfo subscription)
    {
        subscription.UpdatedUtc = DateTimeOffset.UtcNow;
        _subscriptions.AddOrUpdate(
            subscription.Endpoint,
            _ => subscription,
            (_, existing) =>
            {
                existing.P256Dh = subscription.P256Dh;
                existing.Auth = subscription.Auth;
                existing.Latitude = subscription.Latitude;
                existing.Longitude = subscription.Longitude;
                existing.LocationName = subscription.LocationName;
                existing.Culture = subscription.Culture;
                existing.UpdatedUtc = subscription.UpdatedUtc;
                return existing;
            });
    }

    public bool Remove(string endpoint) => _subscriptions.TryRemove(endpoint, out _);

    public void MarkRainAlertSent(string endpoint, DateTimeOffset sentAtUtc)
    {
        if (_subscriptions.TryGetValue(endpoint, out var subscription))
        {
            subscription.LastRainAlertUtc = sentAtUtc;
            subscription.UpdatedUtc = sentAtUtc;
        }
    }
}

