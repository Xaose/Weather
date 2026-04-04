using Weather.Models;

namespace Weather.Services;

public interface ILocationSearchService
{
    Task<IReadOnlyList<LocationSuggestionDto>> SearchLocationsAsync(string query, int limit = 5, CancellationToken cancellationToken = default);
    Task<LocationSuggestionDto?> ReverseGeocodeAsync(double lat, double lon, int limit = 1, CancellationToken cancellationToken = default);
}