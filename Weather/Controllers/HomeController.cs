using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Weather.Models;
using Weather.Services;

namespace Weather.Controllers;

public class HomeController(WeatherService weatherService, ILocationSearchService locationSearchService) : Controller
{
    private const double DefaultLatitude = 23.8103;
    private const double DefaultLongitude = 90.4125;
    private const string DefaultLocationName = "Dhaka";

    private static string T(string en, string ru, string be)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language switch
        {
            "ru" => ru,
            "be" => be,
            _ => en
        };
    }

    public async Task<IActionResult> Index(
        double? latitude,
        double? longitude,
        string? location,
        CancellationToken cancellationToken = default)
    {
        var model = await BuildDashboardModelAsync(latitude, longitude, location, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Timeline(
        double? latitude,
        double? longitude,
        string? location,
        CancellationToken cancellationToken = default)
    {
        var model = await BuildDashboardModelAsync(latitude, longitude, location, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Chat(
        double? latitude,
        double? longitude,
        string? location,
        CancellationToken cancellationToken = default)
    {
        var model = await BuildDashboardModelAsync(latitude, longitude, location, cancellationToken);
        return View(model);
    }

    private async Task<HomeDashboardViewModel> BuildDashboardModelAsync(
        double? latitude,
        double? longitude,
        string? location,
        CancellationToken cancellationToken)
    {
        var selectedLatitude = latitude ?? DefaultLatitude;
        var selectedLongitude = longitude ?? DefaultLongitude;

        var model = new HomeDashboardViewModel
        {
            Latitude = selectedLatitude,
            Longitude = selectedLongitude,
            LocationName = string.IsNullOrWhiteSpace(location)
                ? (latitude.HasValue && longitude.HasValue ? T("Current location", "Текущее местоположение", "Бягучае месцазнаходжанне") : DefaultLocationName)
                : location.Trim()
        };

        try
        {
            var currentTask = weatherService.GetCurrentWeatherAsync(selectedLatitude, selectedLongitude, cancellationToken);
            var hourlyTask = weatherService.GetDailyForecastAsync(selectedLatitude, selectedLongitude, 2, cancellationToken);
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
                    model.LocationName = T("Current location", "Текущее местоположение", "Бягучае месцазнаходжанне");
                }
            }

            if (model.CurrentWeather == null)
            {
                model.ErrorMessage = T(
                    "Weather data is currently unavailable for this location.",
                    "Данные о погоде сейчас недоступны для этой локации.",
                    "Дадзеныя пра надвор'е зараз недаступныя для гэтага месца.");
            }
        }
        catch
        {
            model.ErrorMessage = T(
                "Weather data is currently unavailable for this location.",
                "Данные о погоде сейчас недоступны для этой локации.",
                "Дадзеныя пра надвор'е зараз недаступныя для гэтага месца.");
        }

        return model;
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
