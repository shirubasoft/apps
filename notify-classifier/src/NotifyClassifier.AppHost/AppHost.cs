var builder = DistributedApplication.CreateBuilder(args);

var classifierMode = string.Equals(builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(builder.Environment.EnvironmentName, "CI", StringComparison.OrdinalIgnoreCase)
    ? "Mock"
    : "CodexCli";

builder.AddProject<Projects.NotifyClassifier_Api>("classifier-api", launchProfileName: "http")
    .WithEnvironment("Classifier__Mode", classifierMode)
    .WithEndpoint("http", endpoint => endpoint.Port = 5080)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
