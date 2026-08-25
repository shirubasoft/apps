using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using NotifyClassifier.Core;

namespace NotifyClassifier.Api.Tests;

public sealed class ClassificationApiTests
{
    [Fact]
    public async Task ClassifyReturnsShapeFromConfiguredSchema()
    {
        await using var factory = new ApiFactory("Mock");
        using var client = factory.CreateClient();
        var request = CreateRequest("""
            {
              "type": "object",
              "properties": {
                "category": { "enum": ["action", "other"] },
                "urgent": { "type": "boolean" }
              },
              "required": ["category", "urgent"],
              "additionalProperties": false
            }
            """);

        using var response = await client.PostAsJsonAsync("/classify", request);
        var result = await response.Content.ReadFromJsonAsync<ClassificationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("gpt-5.6-luna", result.Model);
        Assert.Equal("action", result.Result.GetProperty("category").GetString());
        Assert.False(result.Result.GetProperty("urgent").GetBoolean());
    }

    [Fact]
    public async Task ClassifyRejectsNonObjectSchemaDefinition()
    {
        await using var factory = new ApiFactory("Mock");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/classify", CreateRequest("[]"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClassifyReturnsServiceUnavailableWhenCodexCannotStart()
    {
        await using var factory = new ApiFactory("CodexCli", "codex-command-that-does-not-exist");
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/classify", CreateRequest("{\"type\":\"object\"}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ClassifyReadsStructuredResultFromCodexSubprocess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var command = new TemporaryCodexCommand();
        await using var factory = new ApiFactory("CodexCli", command.Path);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/classify",
            CreateRequest("{\"type\":\"object\",\"properties\":{\"label\":{\"type\":\"string\"}}}"));
        var result = await response.Content.ReadFromJsonAsync<ClassificationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("subprocess", result.Result.GetProperty("label").GetString());
    }

    private static ClassificationRequest CreateRequest(string schemaJson)
    {
        using var schema = JsonDocument.Parse(schemaJson);
        return new ClassificationRequest(
            new NotificationPayload(
                "notification-1",
                "com.example.app",
                "Example",
                "Please review",
                "A notification body",
                DateTimeOffset.Parse("2026-08-25T12:00:00Z", CultureInfo.InvariantCulture)),
            new SchemaPayload("schema-1", "Test schema", schema.RootElement.Clone()));
    }

    private sealed class ApiFactory(string mode, string command = "codex") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Classifier:Mode"] = mode,
                    ["Classifier:Command"] = command,
                    ["Classifier:TimeoutSeconds"] = "5"
                });
            });
        }
    }

    private sealed class TemporaryCodexCommand : IDisposable
    {
        public TemporaryCodexCommand()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"notify-classifier-fake-codex-{Guid.NewGuid():N}.sh");
            File.WriteAllText(
                Path,
                """
                #!/usr/bin/env bash
                set -euo pipefail
                result_path=""
                while [[ $# -gt 0 ]]; do
                  if [[ "$1" == "--output-last-message" ]]; then
                    result_path="$2"
                    shift 2
                  else
                    shift
                  fi
                done
                printf '%s' '{"label":"subprocess"}' > "$result_path"
                """);
            using var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                UseShellExecute = false,
                ArgumentList = { "+x", Path }
            }) ?? throw new InvalidOperationException("Could not start chmod.");
            chmod.WaitForExit();
            if (chmod.ExitCode != 0)
            {
                throw new InvalidOperationException("Could not mark the fake Codex command as executable.");
            }
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
