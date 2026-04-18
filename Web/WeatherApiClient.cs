namespace Web;

public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecast>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecast>("/weatherforecast", cancellationToken))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }

    public Task<WeatherRecommendationResponse?> GetRecommendationAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<WeatherRecommendationResponse>("/weather/ai/recommendation", cancellationToken);

    public async Task<WeatherAgentChatResponse?> SendChatMessageAsync(
        string message,
        IReadOnlyList<WeatherAgentMessage> history,
        CancellationToken cancellationToken = default)
    {
        var request = new WeatherAgentChatRequest(message, history);
        using var response = await httpClient.PostAsJsonAsync("/weather/ai/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WeatherAgentChatResponse>(cancellationToken);
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record WeatherRecommendationResponse(string Recommendation, WeatherForecast[] Forecast);

public record WeatherAgentChatRequest(string Message, IReadOnlyList<WeatherAgentMessage> History);

public record WeatherAgentMessage(string Role, string Content);

public record WeatherAgentChatResponse(string Reply);
