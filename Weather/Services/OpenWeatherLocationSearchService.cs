namespace Weather.Services;
using Weather.Models;
public class OpenWeatherLocationSearchService(IHttpClientFactory? httpClientFactory, OpenWeatherOptions options)
    : ILocationSearchService
{
    private readonly string _apiKey = options.ApiKey ?? throw new ArgumentNullException(nameof(options.ApiKey));

    public async Task<IReadOnlyList<LocationSuggestionDto>> SearchLocationsAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory?.CreateClient() ?? new HttpClient();
        var url = $"http://api.openweathermap.org/geo/1.0/direct?q={Uri.EscapeDataString(query)}&limit={limit}&appid={_apiKey}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return System.Text.Json.JsonSerializer.Deserialize<List<LocationSuggestionDto>>(content) ?? [];
    }
    public async Task<LocationSuggestionDto?> ReverseGeocodeAsync(double lat, double lon, int limit = 1, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory?.CreateClient() ?? new HttpClient();
        var url = $"http://api.openweathermap.org/geo/1.0/reverse?lat={lat}&lon={lon}&limit={limit}&appid={_apiKey}";
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var results = System.Text.Json.JsonSerializer.Deserialize<List<LocationSuggestionDto>>(content);
        return results?.FirstOrDefault();
    }
}