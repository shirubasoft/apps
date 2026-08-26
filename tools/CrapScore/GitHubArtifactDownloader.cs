using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CrapScore;

internal sealed class GitHubArtifactDownloader(HttpClient httpClient) : IDisposable
{
    public static GitHubArtifactDownloader CreateFromEnvironment()
    {
        var apiUrl = Environment.GetEnvironmentVariable("CRAP_SCORE_API_URL") ?? "https://api.github.com";
        var token = Environment.GetEnvironmentVariable("CRAP_SCORE_GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "CRAP_SCORE_GITHUB_TOKEN is required to download a prior-run artifact.");
        }

        var client = new HttpClient
        {
            BaseAddress = new Uri($"{apiUrl.TrimEnd('/')}/", UriKind.Absolute),
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("shared-crap-score");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return new GitHubArtifactDownloader(client);
    }

    public async Task DownloadAsync(
        string repository,
        string commit,
        string outputDirectory,
        string artifactPrefix = "crap-score",
        CancellationToken cancellationToken = default)
    {
        var repositoryParts = repository.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (repositoryParts.Length != 2
            || string.IsNullOrWhiteSpace(commit)
            || string.IsNullOrWhiteSpace(artifactPrefix))
        {
            throw new ArgumentException(
                "Repository must be OWNER/REPOSITORY; commit and artifact prefix must be non-empty.");
        }

        var artifactName = $"{artifactPrefix}-{commit}";
        var requestPath = $"repos/{Uri.EscapeDataString(repositoryParts[0])}/"
            + $"{Uri.EscapeDataString(repositoryParts[1])}/actions/artifacts"
            + $"?name={Uri.EscapeDataString(artifactName)}&per_page=100";
        using var artifactsResponse = await httpClient.GetAsync(requestPath, cancellationToken);
        artifactsResponse.EnsureSuccessStatusCode();
        await using var artifactsStream = await artifactsResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var artifactsDocument = await JsonDocument.ParseAsync(
            artifactsStream,
            cancellationToken: cancellationToken);
        var downloadUrl = SelectArtifactDownloadUrl(artifactsDocument, artifactName, commit)
            ?? throw new InvalidOperationException(
                $"No unexpired '{artifactName}' artifact exists for main commit {commit}.");

        using var archiveResponse = await httpClient.GetAsync(downloadUrl, cancellationToken);
        archiveResponse.EnsureSuccessStatusCode();
        await using var archiveStream = await archiveResponse.Content.ReadAsStreamAsync(cancellationToken);
        await ExtractArchiveAsync(archiveStream, outputDirectory, cancellationToken);
    }

    internal static string? SelectArtifactDownloadUrl(
        JsonDocument document,
        string artifactName,
        string commit) =>
        document.RootElement
            .GetProperty("artifacts")
            .EnumerateArray()
            .Where(artifact =>
                string.Equals(artifact.GetProperty("name").GetString(), artifactName, StringComparison.Ordinal)
                && !artifact.GetProperty("expired").GetBoolean()
                && string.Equals(
                    artifact.GetProperty("workflow_run").GetProperty("head_sha").GetString(),
                    commit,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(artifact => artifact.GetProperty("created_at").GetDateTimeOffset())
            .Select(artifact => artifact.GetProperty("archive_download_url").GetString())
            .FirstOrDefault(url => url is not null);

    internal static async Task ExtractArchiveAsync(
        Stream archiveStream,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        await using var seekableArchive = new MemoryStream();
        await archiveStream.CopyToAsync(seekableArchive, cancellationToken);
        seekableArchive.Position = 0;

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);
        var outputRootPrefix = outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? outputRoot
            : $"{outputRoot}{Path.DirectorySeparatorChar}";
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        using var archive = new ZipArchive(seekableArchive, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(outputRoot, entry.FullName));
            if (!destination.StartsWith(outputRootPrefix, pathComparison))
            {
                throw new InvalidDataException($"Artifact contains an unsafe path: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var entryStream = entry.Open();
            await using var destinationStream = File.Create(destination);
            await entryStream.CopyToAsync(destinationStream, cancellationToken);
        }
    }

    public void Dispose() => httpClient.Dispose();
}
