namespace CrapScore.Tests;

public sealed class CoverageInputsTests
{
    [Fact]
    public void ResolveAcceptsExplicitFilesAndRecursivelySearchesDirectories()
    {
        using var directory = new TemporaryDirectory();
        var nestedDirectory = Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var discovered = Path.Combine(nestedDirectory, "coverage.cobertura.xml");
        var explicitReport = Path.Combine(directory.Path, "custom.xml");
        File.WriteAllText(discovered, "<coverage />");
        File.WriteAllText(explicitReport, "<coverage />");

        var resolved = CoverageInputs.Resolve([directory.Path, explicitReport]);

        Assert.Equal(2, resolved.Count);
        Assert.Contains(Path.GetFullPath(discovered), resolved);
        Assert.Contains(Path.GetFullPath(explicitReport), resolved);
    }

    [Fact]
    public void ResolveDeduplicatesReports()
    {
        using var directory = new TemporaryDirectory();
        var report = Path.Combine(directory.Path, "coverage.cobertura.xml");
        File.WriteAllText(report, "<coverage />");

        var resolved = CoverageInputs.Resolve([directory.Path, report]);

        Assert.Single(resolved);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"crap-score-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
