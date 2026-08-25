using NotifyClassifier.Core;

namespace NotifyClassifier.Storage;

public sealed record StoredSchema(
    string Id,
    string Name,
    string JsonSchema,
    bool IsDefault,
    int Version,
    DateTimeOffset UpdatedAt);

public sealed record StoredAppSelection(
    string PackageName,
    string DisplayName,
    string SchemaId,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);

public sealed record StoredNotification(
    string Id,
    string PackageName,
    string AppName,
    string? Title,
    string? Text,
    DateTimeOffset PostedAt,
    string SchemaId,
    string SchemaName,
    string SchemaJson,
    NotificationState State,
    int AttemptCount,
    DateTimeOffset NextAttemptAt,
    string? ResultJson,
    string? LastError,
    DateTimeOffset CreatedAt);

public sealed record QueueSummary(int Waiting, int Completed);

public interface INotificationRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredSchema>> GetSchemasAsync(CancellationToken cancellationToken = default);

    Task<StoredSchema?> GetSchemaAsync(string id, CancellationToken cancellationToken = default);

    Task<StoredSchema> SaveSchemaAsync(StoredSchema schema, CancellationToken cancellationToken = default);

    Task DeleteSchemaAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredAppSelection>> GetSelectionsAsync(CancellationToken cancellationToken = default);

    Task<StoredAppSelection?> GetEnabledSelectionAsync(string packageName, CancellationToken cancellationToken = default);

    Task SaveSelectionAsync(StoredAppSelection selection, CancellationToken cancellationToken = default);

    Task EnqueueAsync(StoredNotification notification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredNotification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(string id, string resultJson, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(string id, string failureMessage, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredNotification>> GetHistoryAsync(int limit, CancellationToken cancellationToken = default);

    Task<QueueSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
