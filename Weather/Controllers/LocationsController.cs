using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Weather.Services;

namespace Weather.Controllers;

[ApiController]
[Route("api/locations")]
public partial class LocationsController(ILocationSearchService locationSearchService) : ControllerBase
{
    private static readonly Regex QueryRegex = MyRegex();
    [GeneratedRegex(@"^[\p{L}\p{M}\s\.\-',]{1,80}$", RegexOptions.Compiled)] private static partial Regex MyRegex();

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

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(query) || !QueryRegex.IsMatch(query)) return BadRequest(
            T(
                "Invalid query. Allowed: letters, spaces, dot, comma, hyphen and apostrophe. Up to 80 characters.",
                "Некорректный запрос. Разрешены буквы, пробел, точка, запятая, дефис и апостроф. До 80 символов.",
                "Некарэктны запыт. Дазволены літары, прабел, кропка, коска, дэфіс і апостраф. Да 80 сімвалаў."));
        var result = await locationSearchService.SearchLocationsAsync(query.Trim(), Math.Clamp(limit, 1, 10), cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-coordinates")]
    public async Task<IActionResult> ByCoordinates([FromQuery] double latitude, [FromQuery] double longitude,
        CancellationToken cancellationToken = default)
    {
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            return BadRequest(T("Invalid coordinates.", "Некорректные координаты.", "Некарэктныя каардынаты."));
        var result = await locationSearchService.ReverseGeocodeAsync(latitude, longitude, 1,  cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
