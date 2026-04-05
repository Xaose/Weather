using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Weather.Models;
using Weather.Services;

namespace Weather.Controllers;

public class HomeController(WeatherService weatherService, ILocationSearchService locationSearchService) : Controller
{
    private const double DefaultLatitude = 23.8103;
    private const double DefaultLongitude = 90.4125;
    private const string DefaultLocationName = "Dhaka";

    public async Task<IActionResult> Index(
        double? latitude,
        double? longitude,
        string? location,
        CancellationToken cancellationToken = default)
    {
        var selectedLatitude = latitude ?? DefaultLatitude;
        var selectedLongitude = longitude ?? DefaultLongitude;

        var model = new HomeDashboardViewModel
        {
            Latitude = selectedLatitude,
            Longitude = selectedLongitude,
            LocationName = string.IsNullOrWhiteSpace(location)
                ? (latitude.HasValue && longitude.HasValue ? "Current location" : DefaultLocationName)
                : location.Trim()
        };

        try
        {
            var currentTask = weatherService.GetCurrentWeatherAsync(selectedLatitude, selectedLongitude, cancellationToken);
            var hourlyTask = weatherService.GetDailyForecastAsync(selectedLatitude, selectedLongitude, 3, cancellationToken);
            var weeklyTask = weatherService.GetWeeklyForecastAsync(selectedLatitude, selectedLongitude, cancellationToken);

            await Task.WhenAll(currentTask, hourlyTask, weeklyTask);

            model.CurrentWeather = await currentTask;
            model.HourlyForecast = (await hourlyTask) ?? new HourlyForecastDto();
            model.WeeklyForecast = (await weeklyTask) ?? new WeeklyForecastDto();

            try
            {
                var resolvedLocation = await locationSearchService.ReverseGeocodeAsync(selectedLatitude, selectedLongitude, 1, cancellationToken);
                if (resolvedLocation != null)
                {
                    model.LocationName = resolvedLocation.Name ?? model.LocationName;
                    model.StateName = resolvedLocation.State;
                    model.CountryName = resolvedLocation.Country;
                }
            }
            catch
            {
                if (latitude.HasValue && longitude.HasValue && string.IsNullOrWhiteSpace(location))
                {
                    model.LocationName = "Current location";
                }
            }

            if (model.CurrentWeather == null)
            {
                model.ErrorMessage = "Weather data is currently unavailable for this location.";
            }
        }
        catch
        {
            model.ErrorMessage = "Weather data is currently unavailable for this location.";
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
