using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

var chatModelName = builder.Configuration["CHAT_MODELNAME"] ?? "chat";
builder.AddAzureChatCompletionsClient("chat")
    .AddChatClient(chatModelName);
builder.Services.AddSingleton<WeatherAgentClient>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
{
    return CreateForecast(summaries);
})
.WithName("GetWeatherForecast");

app.MapGet("/weather/ai/recommendation", async (WeatherAgentClient weatherAgent, CancellationToken cancellationToken) =>
{
    var forecast = CreateForecast(summaries);
    var recommendation = await weatherAgent.GetRecommendationAsync(forecast, cancellationToken);

    return Results.Ok(new WeatherRecommendationResponse(recommendation, forecast));
})
.WithName("GetWeatherRecommendation");

app.MapPost("/weather/ai/chat", async (WeatherAgentChatRequest request, WeatherAgentClient weatherAgent, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Message is required." });
    }

    var answer = await weatherAgent.ChatAsync(request, cancellationToken);
    return Results.Ok(new WeatherAgentChatResponse(answer));
})
.WithName("ChatWithWeatherAgent");

app.MapDefaultEndpoints();

app.Run();

static WeatherForecast[] CreateForecast(string[] summaries) =>
    Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

internal sealed class WeatherAgentClient(IChatClient chatClient, ILogger<WeatherAgentClient> logger)
{
    private const string AgentInstructions =
        "You are weather-agent. You help analyze weather and give recommendations to the user on activity impact and what to wear. " +
        "Allow and answer follow-up questions from the user. Always ask if the user has any follow-up questions. " +
        "Be concise and informative in your answers.";

    public async Task<string> GetRecommendationAsync(IEnumerable<WeatherForecast> forecast, CancellationToken cancellationToken)
    {
        var forecastText = string.Join(
            Environment.NewLine,
            forecast.Select(f => $"- {f.Date:yyyy-MM-dd}: {f.TemperatureC}C ({f.TemperatureF}F), {f.Summary}"));

        var prompt =
            $"{AgentInstructions}{Environment.NewLine}{Environment.NewLine}" +
            "Provide practical clothing and activity recommendations for this forecast:" +
            $"{Environment.NewLine}{forecastText}";

        return await CompletePromptAsync(prompt, cancellationToken);
    }

    public async Task<string> ChatAsync(WeatherAgentChatRequest request, CancellationToken cancellationToken)
    {
        var historyText = request.History.Count == 0
            ? "(No prior messages)"
            : string.Join(Environment.NewLine, request.History.Select(m => $"{m.Role}: {m.Content}"));

        var prompt =
            $"{AgentInstructions}{Environment.NewLine}{Environment.NewLine}" +
            "Conversation so far:" +
            $"{Environment.NewLine}{historyText}{Environment.NewLine}{Environment.NewLine}" +
            $"User: {request.Message}{Environment.NewLine}" +
            "Assistant:";

        return await CompletePromptAsync(prompt, cancellationToken);
    }

    private async Task<string> CompletePromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        var text = response.Text;

        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        logger.LogWarning("Chat client returned an empty response.");
        return "I couldn't generate a recommendation right now. Do you have any follow-up questions?";
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record WeatherRecommendationResponse(string Recommendation, WeatherForecast[] Forecast);

record WeatherAgentChatRequest(string Message, IReadOnlyList<WeatherAgentMessage> History)
{
    public IReadOnlyList<WeatherAgentMessage> History { get; init; } = History ?? [];
}

record WeatherAgentMessage(string Role, string Content);

record WeatherAgentChatResponse(string Reply);
