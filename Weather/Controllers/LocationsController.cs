using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Weather.Services;

namespace Weather.Controllers;

[ApiController]
[Route("api/locations")]
public partial class LocationsController(ILocationSearchService locationSearchService) : ControllerBase
{
    private static readonly Regex QueryRegex = MyRegex();
    [GeneratedRegex(@"^[\p{L}\s\.\-]{1,50}$", RegexOptions.Compiled)] private static partial Regex MyRegex();

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(query) || !QueryRegex.IsMatch(query)) return BadRequest(
            "Некоректный запрос. Разрешены только кирилица/датиница, пробел, точка, дефис. До 50 символов.");
        var result = await locationSearchService.SearchLocationsAsync(query.Trim(), Math.Clamp(limit, 1, 10), cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-coordinates")]
    public async Task<IActionResult> ByCoordinates([FromQuery] double latitude, [FromQuery] double longitude,
        CancellationToken cancellationToken = default)
    {
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            return BadRequest("Некоректные координаты.");
        var result = await locationSearchService.ReverseGeocodeAsync(latitude, longitude, 1,  cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
