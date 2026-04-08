using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Weather.Controllers;

[ApiController]
[Route("api/assistant")]
public class AssistantController(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
{
    private readonly string _apiKey = configuration["OpenWeather:ApiKey"] ?? throw new InvalidOperationException("OpenWeather API key is not configured.");

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AssistantChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required." });
        }

        var client = httpClientFactory.CreateClient();
        var url = string.IsNullOrWhiteSpace(request.SessionId)
            ? "https://api.openweathermap.org/assistant/session"
            : $"https://api.openweathermap.org/assistant/session/{request.SessionId}";

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Add("X-Api-Key", _apiKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(new { prompt = request.Prompt }),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ContentResult
        {
            Content = responseBody,
            ContentType = "application/json; charset=utf-8",
            StatusCode = (int)response.StatusCode
        };
    }

    public sealed record AssistantChatRequest(string Prompt, string? SessionId);
}
