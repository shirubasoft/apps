using System.Text.Json;

using NotifyClassifier.Core;

namespace NotifyClassifier.Api;

internal interface INotificationClassifier
{
    Task<JsonElement> ClassifyAsync(ClassificationRequest request, CancellationToken cancellationToken);
}

internal sealed class ClassifierUnavailableException : Exception
{
    public ClassifierUnavailableException(string message)
        : base(message)
    {
    }

    public ClassifierUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class ClassifierOptions
{
    public const string SectionName = "Classifier";

    public string Mode { get; set; } = "CodexCli";

    public string Model { get; set; } = "gpt-5.6-luna";

    public string Command { get; set; } = "codex";

    public int TimeoutSeconds { get; set; } = 180;
}
