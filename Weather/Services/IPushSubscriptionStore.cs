using Weather.Models;

namespace Weather.Services;

public interface IPushSubscriptionStore
{
    IReadOnlyCollection<PushSubscriptionInfo> GetAll();
    void Upsert(PushSubscriptionInfo subscription);
    bool Remove(string endpoint);
    void MarkRainAlertSent(string endpoint, DateTimeOffset sentAtUtc);
}

