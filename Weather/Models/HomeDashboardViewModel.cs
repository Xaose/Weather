namespace Weather.Models;

public class HomeDashboardViewModel
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationName { get; set; } = "";
    public string? StateName { get; set; }
    public string? CountryName { get; set; }
    public CurrentWeatherDto? CurrentWeather { get; set; }
    public HourlyForecastDto HourlyForecast { get; set; } = new();
    public WeeklyForecastDto WeeklyForecast { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public string LocationDisplayName => string.Join(", ", new[] { LocationName, StateName, CountryName }
        .Where(static value => !string.IsNullOrWhiteSpace(value)));
}
