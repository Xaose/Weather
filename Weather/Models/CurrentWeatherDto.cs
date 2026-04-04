namespace Weather.Models;

public class CurrentWeatherDto
{
      public double TemperatureC { get; set; }
      public double TemperatureF { get; set; }
      public double FeelsLike { get; set; }
      
      public string? Description { get; set; }
      
      public string? Condition { get; set; }
      public string? IconUrl { get; set; }
      
      public int Humidity { get; set; }
      public int PressureHpa { get; set; }
      public int PressureMmHg { get; set; }
      
      public double WindSpeedKmh { get; set; }
      public  double WindSpeedMs { get; set; }
      public int WindDirectionDeg { get; set; }
      public string? WindDirectionText { get; set; }
            
}