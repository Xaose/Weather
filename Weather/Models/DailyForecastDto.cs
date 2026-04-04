namespace Weather.Models;

public class DailyForecastDto
{
    public DateTime Date { get; set; }
    public double DayTemperatureC { get; set; }
    public double NightTemperatureC { get; set; }
    public double DayTemperatureF { get; set; }
    public double NightTemperatureF { get; set; }
    public int PrecipitationProbabilityPercent { get; set; }
    public string? Condition { get; set; }
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public double WindSpeedMs { get; set; }
    public double WindSpeedKmh { get; set; }
}