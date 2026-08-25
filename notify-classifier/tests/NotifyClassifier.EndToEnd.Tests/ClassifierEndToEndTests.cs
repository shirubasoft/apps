using System.Text.Json;

using NotifyClassifier.Core;

namespace NotifyClassifier.EndToEnd.Tests;

public sealed class ClassifierEndToEndTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task AppHostStartsClassifierApiAndClassifiesWithCiProvider()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.NotifyClassifier_AppHost>(
            [],
            (_, settings) => settings.EnvironmentName = "Testing",
            cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(StartupTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("classifier-api", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        using var client = app.CreateHttpClient("classifier-api");
        using var schema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": { "label": { "enum": ["e2e"] } },
              "required": ["label"],
              "additionalProperties": false
            }
            """);
        var request = new ClassificationRequest(
            new NotificationPayload(
                "e2e-notification",
                "com.example.app",
                "Example",
                "End-to-end",
                "Classify this",
                DateTimeOffset.UnixEpoch),
            new SchemaPayload("e2e-schema", "E2E", schema.RootElement.Clone()));

        using var response = await client.PostAsJsonAsync("/classify", request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ClassificationResponse>(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("e2e", result.Result.GetProperty("label").GetString());
    }
}
