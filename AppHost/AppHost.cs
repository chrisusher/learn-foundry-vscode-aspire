var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Foundry_VSC_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Foundry_VSC_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
