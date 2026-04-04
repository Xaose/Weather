namespace Weather.Models;

public class WeeklyForecastDto
{
    public List<DailyForecastDto> DailyForecasts { get; set; } = new ();
}