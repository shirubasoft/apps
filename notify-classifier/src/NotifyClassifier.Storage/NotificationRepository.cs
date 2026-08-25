using NotifyClassifier.Core;

using SQLite;

namespace NotifyClassifier.Storage;

public sealed class NotificationRepository : INotificationRepository, IDisposable
{
    public const string DefaultSchemaId = "00000000-0000-0000-0000-000000000001";

    private const string DefaultSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "category": { "type": "string", "enum": ["action", "important", "social", "promotion", "other"] },
            "urgent": { "type": "boolean" },
            "summary": { "type": "string" }
          },
          "required": ["category", "urgent", "summary"],
          "additionalProperties": false
        }
        """;

    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public NotificationRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var connectionString = new SQLiteConnectionString(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
            true);
        _connection = new SQLiteAsyncConnection(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                return true;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<StoredSchema>> GetSchemasAsync(CancellationToken cancellationToken = default) =>
        InLockAsync<IReadOnlyList<StoredSchema>>(
            async () =>
            {
                await EnsureInitializedAsync();
                var rows = await _connection.Table<SchemaRow>()
                    .OrderByDescending(row => row.IsDefault)
                    .ThenBy(row => row.Name)
                    .ToListAsync();
                return rows.Select(ToModel).ToArray();
            },
            cancellationToken);

    public Task<StoredSchema?> GetSchemaAsync(string id, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var row = await _connection.FindAsync<SchemaRow>(id);
                return row is null ? null : ToModel(row);
            },
            cancellationToken);

    public Task<StoredSchema> SaveSchemaAsync(StoredSchema schema, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var validation = SchemaTools.ValidateDefinition(schema.JsonSchema);
                if (!validation.IsValid)
                {
                    throw new ArgumentException(validation.Error, nameof(schema));
                }

                var normalized = schema with
                {
                    Id = string.IsNullOrWhiteSpace(schema.Id) ? Guid.NewGuid().ToString() : schema.Id,
                    Name = schema.Name.Trim(),
                    UpdatedAt = schema.UpdatedAt == default ? DateTimeOffset.UtcNow : schema.UpdatedAt,
                    Version = Math.Max(schema.Version, 1)
                };

                if (normalized.IsDefault)
                {
                    await _connection.ExecuteAsync("UPDATE SchemaRow SET IsDefault = 0");
                }

                await _connection.InsertOrReplaceAsync(ToRow(normalized));
                return normalized;
            },
            cancellationToken);

    public Task DeleteSchemaAsync(string id, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var schema = await _connection.FindAsync<SchemaRow>(id);
                if (schema is null)
                {
                    return true;
                }

                if (schema.IsDefault)
                {
                    throw new InvalidOperationException("The default schema cannot be deleted.");
                }

                var defaultSchema = await GetDefaultSchemaRowAsync();
                await _connection.ExecuteAsync(
                    "UPDATE AppSelectionRow SET SchemaId = ? WHERE SchemaId = ?",
                    defaultSchema.Id,
                    id);
                await _connection.DeleteAsync(schema);
                return true;
            },
            cancellationToken);

    public Task<IReadOnlyList<StoredAppSelection>> GetSelectionsAsync(CancellationToken cancellationToken = default) =>
        InLockAsync<IReadOnlyList<StoredAppSelection>>(
            async () =>
            {
                await EnsureInitializedAsync();
                var rows = await _connection.Table<AppSelectionRow>().ToListAsync();
                return rows.Select(ToModel).ToArray();
            },
            cancellationToken);

    public Task<StoredAppSelection?> GetEnabledSelectionAsync(string packageName, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var row = await _connection.Table<AppSelectionRow>()
                    .Where(selection => selection.PackageName == packageName && selection.IsEnabled)
                    .FirstOrDefaultAsync();
                return row is null ? null : ToModel(row);
            },
            cancellationToken);

    public Task SaveSelectionAsync(StoredAppSelection selection, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var schema = await _connection.FindAsync<SchemaRow>(selection.SchemaId);
                var schemaId = schema?.Id ?? (await GetDefaultSchemaRowAsync()).Id;
                await _connection.InsertOrReplaceAsync(new AppSelectionRow
                {
                    PackageName = selection.PackageName,
                    DisplayName = selection.DisplayName,
                    SchemaId = schemaId,
                    IsEnabled = selection.IsEnabled,
                    UpdatedAtUnixMilliseconds = selection.UpdatedAt.ToUnixTimeMilliseconds()
                });
                return true;
            },
            cancellationToken);

    public Task EnqueueAsync(StoredNotification notification, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                await _connection.InsertAsync(ToRow(notification), "OR IGNORE");
                return true;
            },
            cancellationToken);

    public Task<IReadOnlyList<StoredNotification>> ClaimDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default) =>
        InLockAsync<IReadOnlyList<StoredNotification>>(
            async () =>
            {
                await EnsureInitializedAsync();
                var timestamp = now.ToUnixTimeMilliseconds();
                var rows = await _connection.QueryAsync<NotificationRow>(
                    """
                    SELECT * FROM NotificationRow
                    WHERE State IN (?, ?, ?) AND NextAttemptAtUnixMilliseconds <= ?
                    ORDER BY CreatedAtUnixMilliseconds
                    LIMIT ?
                    """,
                    (int)NotificationState.Pending,
                    (int)NotificationState.RetryScheduled,
                    (int)NotificationState.Processing,
                    timestamp,
                    Math.Max(limit, 1));

                var leaseUntil = now.AddMinutes(10).ToUnixTimeMilliseconds();
                foreach (var row in rows)
                {
                    row.State = (int)NotificationState.Processing;
                    row.NextAttemptAtUnixMilliseconds = leaseUntil;
                    await _connection.UpdateAsync(row);
                }

                return rows.Select(ToModel).ToArray();
            },
            cancellationToken);

    public Task MarkCompletedAsync(string id, string resultJson, CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                await _connection.ExecuteAsync(
                    "UPDATE NotificationRow SET State = ?, ResultJson = ?, LastError = NULL WHERE Id = ?",
                    (int)NotificationState.Completed,
                    resultJson,
                    id);
                return true;
            },
            cancellationToken);

    public Task MarkFailedAsync(
        string id,
        string failureMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var row = await _connection.FindAsync<NotificationRow>(id);
                if (row is null)
                {
                    return true;
                }

                row.AttemptCount++;
                row.State = (int)NotificationState.RetryScheduled;
                row.LastError = failureMessage.Length <= 4000 ? failureMessage : failureMessage[..4000];
                row.NextAttemptAtUnixMilliseconds = now.Add(RetryPolicy.GetDelay(row.AttemptCount)).ToUnixTimeMilliseconds();
                await _connection.UpdateAsync(row);
                return true;
            },
            cancellationToken);

    public Task<IReadOnlyList<StoredNotification>> GetHistoryAsync(int limit, CancellationToken cancellationToken = default) =>
        InLockAsync<IReadOnlyList<StoredNotification>>(
            async () =>
            {
                await EnsureInitializedAsync();
                var rows = await _connection.Table<NotificationRow>()
                    .OrderByDescending(row => row.CreatedAtUnixMilliseconds)
                    .Take(Math.Max(limit, 1))
                    .ToListAsync();
                return rows.Select(ToModel).ToArray();
            },
            cancellationToken);

    public Task<QueueSummary> GetSummaryAsync(CancellationToken cancellationToken = default) =>
        InLockAsync(
            async () =>
            {
                await EnsureInitializedAsync();
                var completed = await _connection.Table<NotificationRow>()
                    .Where(row => row.State == (int)NotificationState.Completed)
                    .CountAsync();
                var total = await _connection.Table<NotificationRow>().CountAsync();
                return new QueueSummary(total - completed, completed);
            },
            cancellationToken);

    public void Dispose()
    {
        _connection.CloseAsync().GetAwaiter().GetResult();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _connection.CreateTableAsync<SchemaRow>();
        await _connection.CreateTableAsync<AppSelectionRow>();
        await _connection.CreateTableAsync<NotificationRow>();

        if (await _connection.Table<SchemaRow>().CountAsync() == 0)
        {
            await _connection.InsertAsync(new SchemaRow
            {
                Id = DefaultSchemaId,
                Name = "Notification triage",
                JsonSchema = DefaultSchemaJson,
                IsDefault = true,
                Version = 1,
                UpdatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        _initialized = true;
    }

    private async Task<SchemaRow> GetDefaultSchemaRowAsync()
    {
        var schema = await _connection.Table<SchemaRow>()
            .Where(row => row.IsDefault)
            .FirstOrDefaultAsync();
        return schema ?? throw new InvalidOperationException("No default schema is configured.");
    }

    private async Task<T> InLockAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static StoredSchema ToModel(SchemaRow row) => new(
        row.Id,
        row.Name,
        row.JsonSchema,
        row.IsDefault,
        row.Version,
        DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAtUnixMilliseconds));

    private static SchemaRow ToRow(StoredSchema schema) => new()
    {
        Id = schema.Id,
        Name = schema.Name,
        JsonSchema = schema.JsonSchema,
        IsDefault = schema.IsDefault,
        Version = schema.Version,
        UpdatedAtUnixMilliseconds = schema.UpdatedAt.ToUnixTimeMilliseconds()
    };

    private static StoredAppSelection ToModel(AppSelectionRow row) => new(
        row.PackageName,
        row.DisplayName,
        row.SchemaId,
        row.IsEnabled,
        DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAtUnixMilliseconds));

    private static StoredNotification ToModel(NotificationRow row) => new(
        row.Id,
        row.PackageName,
        row.AppName,
        row.Title,
        row.Text,
        DateTimeOffset.FromUnixTimeMilliseconds(row.PostedAtUnixMilliseconds),
        row.SchemaId,
        row.SchemaName,
        row.SchemaJson,
        (NotificationState)row.State,
        row.AttemptCount,
        DateTimeOffset.FromUnixTimeMilliseconds(row.NextAttemptAtUnixMilliseconds),
        row.ResultJson,
        row.LastError,
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAtUnixMilliseconds));

    private static NotificationRow ToRow(StoredNotification notification) => new()
    {
        Id = notification.Id,
        PackageName = notification.PackageName,
        AppName = notification.AppName,
        Title = notification.Title,
        Text = notification.Text,
        PostedAtUnixMilliseconds = notification.PostedAt.ToUnixTimeMilliseconds(),
        SchemaId = notification.SchemaId,
        SchemaName = notification.SchemaName,
        SchemaJson = notification.SchemaJson,
        State = (int)notification.State,
        AttemptCount = notification.AttemptCount,
        NextAttemptAtUnixMilliseconds = notification.NextAttemptAt.ToUnixTimeMilliseconds(),
        ResultJson = notification.ResultJson,
        LastError = notification.LastError,
        CreatedAtUnixMilliseconds = notification.CreatedAt.ToUnixTimeMilliseconds()
    };

    [Table("SchemaRow")]
    private sealed class SchemaRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string JsonSchema { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public int Version { get; set; }

        public long UpdatedAtUnixMilliseconds { get; set; }
    }

    [Table("AppSelectionRow")]
    private sealed class AppSelectionRow
    {
        [PrimaryKey]
        public string PackageName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string SchemaId { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public long UpdatedAtUnixMilliseconds { get; set; }
    }

    [Table("NotificationRow")]
    private sealed class NotificationRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        [Indexed]
        public string PackageName { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Text { get; set; }

        public long PostedAtUnixMilliseconds { get; set; }

        public string SchemaId { get; set; } = string.Empty;

        public string SchemaName { get; set; } = string.Empty;

        public string SchemaJson { get; set; } = string.Empty;

        [Indexed]
        public int State { get; set; }

        public int AttemptCount { get; set; }

        [Indexed]
        public long NextAttemptAtUnixMilliseconds { get; set; }

        public string? ResultJson { get; set; }

        public string? LastError { get; set; }

        public long CreatedAtUnixMilliseconds { get; set; }
    }
}
