using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace CrapScore.Tests;

public sealed class GitHubArtifactDownloaderTests
{
    [Fact]
    public void SelectArtifactDownloadUrlChoosesNewestMatchingRun()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "artifacts": [
                {
                  "name": "crap-score-abc",
                  "expired": false,
                  "created_at": "2026-08-24T00:00:00Z",
                  "archive_download_url": "https://example.invalid/old",
                  "workflow_run": { "head_sha": "abc" }
                },
                {
                  "name": "crap-score-abc",
                  "expired": false,
                  "created_at": "2026-08-25T00:00:00Z",
                  "archive_download_url": "https://example.invalid/new",
                  "workflow_run": { "head_sha": "ABC" }
                }
              ]
            }
            """);

        var url = GitHubArtifactDownloader.SelectArtifactDownloadUrl(document, "crap-score-abc", "abc");

        Assert.Equal("https://example.invalid/new", url);
    }

    [Fact]
    public async Task ExtractArchiveRejectsParentTraversal()
    {
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../outside.txt");
            await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            await writer.WriteAsync("unsafe");
        }

        stream.Position = 0;
        var output = Path.Combine(Path.GetTempPath(), $"crap-score-archive-{Guid.NewGuid():N}");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => GitHubArtifactDownloader.ExtractArchiveAsync(stream, output));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }
}
