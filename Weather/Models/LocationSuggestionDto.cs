namespace Weather.Models;

public class LocationSuggestionDto
{
    public string? Name { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
}