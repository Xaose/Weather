namespace Weather.Models;

public sealed class PushNotificationOptions
{
    public bool Enabled { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 30;
    public int RainProbabilityThresholdPercent { get; set; } = 60;
    public int AlertCooldownHours { get; set; } = 12;
    public string VapidPublicKey { get; set; } = "";
    public string VapidPrivateKey { get; set; } = "";
    public string VapidSubject { get; set; } = "mailto:weather@example.com";
}

