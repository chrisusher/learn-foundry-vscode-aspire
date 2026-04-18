using Aspire.Hosting;
using Aspire.Hosting.Foundry;

var builder = DistributedApplication.CreateBuilder(args);

var foundry = builder.AddFoundry("foundry");
var project = foundry.AddProject("weather-agents");
var chat = foundry.AddDeployment("chat", FoundryModel.OpenAI.Gpt54Mini);
var agent = project.AddAndPublishPromptAgent(chat, "weather-agent", "You help analyse weather and give recommendations to the user on impact on activities, what to wear, etc.  Allow and answer follow-up questions from the user. Always ask if the user has any follow-up questions. Be concise and informative in your answers.")
    .WithOtlpExporter();

var apiService = builder.AddProject<Projects.API>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(chat)
    .WaitFor(agent)
    .WithOtlpExporter();

builder.AddProject<Projects.Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithOtlpExporter();

builder.Build().Run();
