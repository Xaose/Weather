namespace Weather.Models;

public class HourlyForecastDto
{
    public string TimeZone { get; set; } = "";
    public List<HourlyForecastEntryDto> HourlyForecasts { get; set; } = new();
}

public class HourlyForecastEntryDto
{
    public DateTime TimeUts { get; set; }
    public double TemperatureC { get; set; }
    public double TemperatureF { get; set; }
    public int PrecipitationProbabilityPercent { get; set; }
    public string? Condition { get; set; }
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public double WindSpeedMs { get; set; }
    public double WindSpeedKmh { get; set; }
}