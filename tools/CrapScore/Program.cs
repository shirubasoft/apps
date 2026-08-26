using System.Text.Json;
using System.Xml;

namespace CrapScore;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length is 4 or 5
            && string.Equals(args[0], "download-artifact", StringComparison.Ordinal))
        {
            var artifactPrefix = args.Length == 5 ? args[4] : "crap-score";
            return await DownloadArtifactAsync(args[1], args[2], args[3], artifactPrefix);
        }

        if (!CommandLine.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(CommandLine.Usage);
            return 2;
        }

        try
        {
            var methods = await CoverageInputs.ReadAsync(options.ReportInputs);
            var report = CrapReportRenderer.Render(methods, options.Target);
            Console.Write(report);
            if (options.OutputPath is not null)
            {
                var outputPath = Path.GetFullPath(options.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(outputPath, report);
            }

            if (options.BaseReportInputs.Count == 0)
            {
                return 0;
            }

            var baseMethods = await CoverageInputs.ReadAsync(options.BaseReportInputs);
            var gate = CrapGate.Evaluate(methods[0].Score, baseMethods[0].Score, options.Target);
            Console.WriteLine(gate.Message);
            return gate.Passed ? 0 : 1;
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static async Task<int> DownloadArtifactAsync(
        string repository,
        string commit,
        string outputDirectory,
        string artifactPrefix)
    {
        try
        {
            using var downloader = GitHubArtifactDownloader.CreateFromEnvironment();
            await downloader.DownloadAsync(repository, commit, outputDirectory, artifactPrefix);
            Console.WriteLine($"Downloaded CRAP score artifact for {commit} to {outputDirectory}.");
            return 0;
        }
        catch (Exception exception) when (IsDownloadFailure(exception))
        {
            Console.Error.WriteLine(exception.Message);
            return 3;
        }
    }

    private static bool IsInputFailure(Exception exception) => exception is
        ArgumentException or
        IOException or
        UnauthorizedAccessException or
        XmlException;

    private static bool IsDownloadFailure(Exception exception) =>
        IsInputFailure(exception)
        || exception is FormatException or HttpRequestException or InvalidOperationException or JsonException;
}
