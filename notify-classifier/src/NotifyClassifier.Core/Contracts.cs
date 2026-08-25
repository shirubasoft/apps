using System.Text.Json;

namespace NotifyClassifier.Core;

public enum NotificationState
{
    Pending,
    Processing,
    RetryScheduled,
    Completed
}

public sealed record InstalledApp(string PackageName, string DisplayName);

public sealed record NotificationPayload(
    string Id,
    string PackageName,
    string AppName,
    string? Title,
    string? Text,
    DateTimeOffset PostedAt);

public sealed record SchemaPayload(string Id, string Name, JsonElement Definition);

public sealed record ClassificationRequest(NotificationPayload Notification, SchemaPayload Schema);

public sealed record ClassificationResponse(
    string NotificationId,
    string SchemaId,
    string SchemaName,
    string Model,
    JsonElement Result);

public sealed record SchemaValidationResult(bool IsValid, string? Error)
{
    public static SchemaValidationResult Valid { get; } = new(true, null);

    public static SchemaValidationResult Invalid(string error) => new(false, error);
}
