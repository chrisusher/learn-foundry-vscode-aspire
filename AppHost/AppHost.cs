var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.API>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
