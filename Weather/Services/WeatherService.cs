using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Weather.Models;

namespace Weather.Services;

public partial class WeatherService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
{
    private readonly string _apiKey = configuration["OpenWeather:ApiKey"] ?? throw new InvalidOperationException("OpenWeather API key is not configured.");

    private string ResolveOpenWeatherLanguage()
    {
        var language = httpContextAccessor.HttpContext?.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?.RequestCulture.UICulture.TwoLetterISOLanguageName
            ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        return language switch
        {
            "ru" => "ru",
            "be" => "ru",
            _ => "en"
        };
    }

    public async Task<CurrentWeatherDto?> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var language = ResolveOpenWeatherLanguage();
        var url = $"https://api.openweathermap.org/data/3.0/onecall?lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}&appid={_apiKey}&units=metric&lang={language}";
        var response = await httpClientFactory.CreateClient().GetAsync(url, cancellationToken);
        
        if (response is null) throw new InvalidOperationException("Ошибка при получении данных о погоде."); 
        
        var data = await response.Content.ReadFromJsonAsync<WeatherResponse>(cancellationToken: cancellationToken);

        if (data == null) throw new InvalidOperationException("Ошибка десериализации.");

        var current = data.Current;
        var weather = current.Weather.FirstOrDefault();

        return new CurrentWeatherDto
        {
            TemperatureC = current.Temp,
            TemperatureF = current.Temp * 9 / 5 + 32,
            FeelsLike = current.FeelsLike,
            Description = weather?.Description,
            Condition = weather?.Main,
            IconUrl = weather != null ? $"https://openweathermap.org/img/wn/{weather?.Icon}@2x.png" : null,
            Humidity = current.Humidity,
            PressureHpa = current.Pressure,
            PressureMmHg = (int)(current.Pressure * 0.750062),
            WindSpeedKmh = current.WindSpeed * 3.6,
            WindSpeedMs = current.WindSpeed,
            WindDirectionDeg = current.WindDeg,
            WindDirectionText = GetWindDirection(current.WindDeg, language)
        };
    }

    private static string GetWindDirection(int degrees, string language)
    {
        var directionCode = degrees switch
        {
            >= 337 or < 22 => "N",
            >= 22 and < 67 => "NE",
            >= 67 and < 112 => "E",
            >= 112 and < 157 => "SE",
            >= 157 and < 202 => "S",
            >= 202 and < 247 => "SW",
            >= 247 and < 292 => "W",
            _ => "NW"
        };

        return language switch
        {
            "ru" => directionCode switch
            {
                "N" => "Север",
                "NE" => "СВ",
                "E" => "Восток",
                "SE" => "ЮВ",
                "S" => "Юг",
                "SW" => "ЮЗ",
                "W" => "Запад",
                _ => "СЗ"
            },
            "be" => directionCode switch
            {
                "N" => "Поўнач",
                "NE" => "ПнУсход",
                "E" => "Усход",
                "SE" => "ПдУсход",
                "S" => "Поўдзень",
                "SW" => "ПдЗахад",
                "W" => "Захад",
                _ => "ПнЗахад"
            },
            _ => directionCode switch
            {
                "N" => "North",
                "NE" => "NE",
                "E" => "East",
                "SE" => "SE",
                "S" => "South",
                "SW" => "SW",
                "W" => "West",
                _ => "NW"
            }
        };
    }

    public async Task<WeeklyForecastDto?> GetWeeklyForecastAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        var language = ResolveOpenWeatherLanguage();

        var tasks = Enumerable.Range(0,7).Select(async i =>
        {
            var date = DateTime
                .UtcNow
                .Date
                .AddDays(i);
            var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var latKey = Math.Round(latitude,3).ToString(CultureInfo.InvariantCulture);
            var lonKey = Math.Round(longitude,3).ToString(CultureInfo.InvariantCulture);
            var cacheKey = $"day-summary:{latKey}:{lonKey}:{language}:{dateStr}";

            var response = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20);

                var url =
                    $"https://api.openweathermap.org/data/3.0/onecall/day_summary" +
                    $"?lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
                    $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}" +
                    $"&date={dateStr}" +
                    $"&appid={_apiKey}&units=metric&lang={language}";

                return await client.GetFromJsonAsync<DaySummaryResponse>(url, cancellationToken);
            });

            if (response == null) return null;

            return new DailyForecastDto {
                Date = DateTime.Parse((ReadOnlySpan<char>)response.Date, CultureInfo.InvariantCulture),
                DayTemperatureC = response.Temperature.Afternoon,
                NightTemperatureC = response.Temperature.Night,
                DayTemperatureF = response.Temperature.Afternoon *9 /5 +32,
                NightTemperatureF = response.Temperature.Night *9 /5 +32,
                PrecipitationProbabilityPercent =0,
                Condition = null,
                Description = null,
                IconUrl = null,
                WindSpeedMs = response.Wind.Max.Speed,
                WindSpeedKmh = response.Wind.Max.Speed *3.6 };
        });

        var results = await Task.WhenAll(tasks);

        return new WeeklyForecastDto {
            DailyForecasts = results .Where(x => x != null)
                .Select(x => x!)
                .OrderBy(x => x.Date)
                .ToList()
        };
    }
public async Task<HourlyForecastDto?> GetDailyForecastAsync(
        double latitude,
        double longitude,
        int stepHours = 1,
        CancellationToken cancellationToken = default)
    {
        stepHours = Math.Clamp(stepHours, 1, 3);
        var language = ResolveOpenWeatherLanguage();
        var latKey = Math.Round(latitude,3).ToString(CultureInfo.InvariantCulture);
        var lonKey = Math.Round(longitude,3).ToString(CultureInfo.InvariantCulture);
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 600;
        var cacheKey = $"hourly:{latKey}:{lonKey}:{language}:{stepHours}:{bucket}";
        var data = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            var url =
                $"https://api.openweathermap.org/data/3.0/onecall" +
                $"?lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
                $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}" +
                $"&exclude=current,minutely,daily,alerts" +
                $"&appid={_apiKey}&units=metric&lang={language}";

            return await httpClientFactory.CreateClient()
                .GetFromJsonAsync<HourlyOneCallResponse>(url, cancellationToken);
        });
        if (data?.Hourly == null || data.Hourly.Count == 0) return null;
        
        var now = DateTimeOffset.UtcNow;
        var end = now.AddHours(24);
        var entries = data.Hourly.Where(h =>
        {
            var t = DateTimeOffset.FromUnixTimeSeconds(h.Dt);
            return t >= now && t <= end;
        })
        .OrderBy(h => h.Dt)
        .Where((_ , index) => index % stepHours == 0)
        .Select(h =>
        {
            var weather = h.Weather.FirstOrDefault();
            return new HourlyForecastEntryDto
            {
                TimeUts = DateTimeOffset.FromUnixTimeSeconds(h.Dt).DateTime,
                TemperatureC = h.Temp,
                TemperatureF = h.Temp * 9 / 5 + 32,
                PrecipitationProbabilityPercent = (int)(h.Pop * 100),
                Condition = weather?.Main,
                Description = weather?.Description,
                IconUrl = weather != null ? $"https://openweathermap.org/img/wn/{weather.Icon}@2x.png" : null,
                WindSpeedMs = h.WindSpeed,
                WindSpeedKmh = h.WindSpeed * 3.6
            };
        })
        .ToList();

        return new HourlyForecastDto
        {
            TimeZone = data.Timezone,
            HourlyForecasts = entries
        };
    }
    
    
    private sealed class DaySummaryResponse
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = "";

        [JsonPropertyName("temperature")]
        public Temperature Temperature { get; set; } = new();

        [JsonPropertyName("wind")]
        public Wind Wind { get; set; } = new();
    }

    private sealed class Temperature
    {
        [JsonPropertyName("afternoon")]
        public double Afternoon { get; set; }

        [JsonPropertyName("night")]
        public double Night { get; set; }
    }

    private sealed class Wind
    {
        [JsonPropertyName("max")]
        public WindMax Max { get; set; } = new();
    }

    private sealed class WindMax
    {
        [JsonPropertyName("speed")]
        public double Speed { get; set; }
    }
    
    private sealed class HourlyOneCallResponse{
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = "";

        [JsonPropertyName("hourly")]
        public List<HourlyEntry> Hourly { get; set; } = new();
    }

    private sealed class HourlyEntry
    {
        [JsonPropertyName("dt")] public long Dt { get; set; }

        [JsonPropertyName("temp")] public double Temp { get; set; }

        [JsonPropertyName("pop")] public double Pop { get; set; }

        [JsonPropertyName("wind_speed")] public double WindSpeed { get; set; }

        [JsonPropertyName("weather")] public List<WeatherInfo> Weather { get; set; } = new();
    }
}

public class WeatherResponse
{
    public Current Current { get; init; } = null!;
}

public class Current
{
    public double Temp { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    public int Pressure { get; set; }
    public int Humidity { get; set; }

    [JsonPropertyName("wind_speed")]
    public double WindSpeed { get; set; }

    [JsonPropertyName("wind_deg")]
    public int WindDeg { get; set; }

    public List<WeatherInfo> Weather { get; set; } = new();
}

public class WeatherInfo
{
    public string Main { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
}
