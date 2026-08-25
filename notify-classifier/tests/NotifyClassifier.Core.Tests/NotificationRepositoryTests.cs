using System.Globalization;

using NotifyClassifier.Storage;

namespace NotifyClassifier.Core.Tests;

public sealed class NotificationRepositoryTests
{
    [Fact]
    public async Task InitializesWithDefaultSchema()
    {
        using var database = new TestDatabase();
        await database.Repository.InitializeAsync();

        var schemas = await database.Repository.GetSchemasAsync();

        var schema = Assert.Single(schemas);
        Assert.True(schema.IsDefault);
        Assert.Equal(NotificationRepository.DefaultSchemaId, schema.Id);
    }

    [Fact]
    public async Task FailedNotificationRemainsQueuedUntilItCompletes()
    {
        using var database = new TestDatabase();
        await database.Repository.InitializeAsync();
        var now = DateTimeOffset.Parse("2026-08-25T12:00:00Z", CultureInfo.InvariantCulture);
        var notification = CreateNotification(now);
        await database.Repository.EnqueueAsync(notification);

        var firstClaim = await database.Repository.ClaimDueAsync(now, 10);
        await database.Repository.MarkFailedAsync(notification.Id, "offline", now);
        var waiting = await database.Repository.GetSummaryAsync();
        var tooEarly = await database.Repository.ClaimDueAsync(now.AddSeconds(29), 10);
        var retry = await database.Repository.ClaimDueAsync(now.AddSeconds(30), 10);
        await database.Repository.MarkCompletedAsync(notification.Id, "{\"label\":\"done\"}");
        var completed = await database.Repository.GetSummaryAsync();
        var history = await database.Repository.GetHistoryAsync(10);

        Assert.Single(firstClaim);
        Assert.Equal(1, waiting.Waiting);
        Assert.Empty(tooEarly);
        Assert.Single(retry);
        Assert.Equal(0, completed.Waiting);
        Assert.Equal(1, completed.Completed);
        Assert.Equal(NotificationState.Completed, Assert.Single(history).State);
    }

    [Fact]
    public async Task DeletingSchemaReassignsSelectedAppsToDefault()
    {
        using var database = new TestDatabase();
        await database.Repository.InitializeAsync();
        var custom = await database.Repository.SaveSchemaAsync(new StoredSchema(
            Guid.NewGuid().ToString(),
            "Custom",
            "{\"type\":\"object\"}",
            false,
            1,
            DateTimeOffset.UtcNow));
        await database.Repository.SaveSelectionAsync(new StoredAppSelection(
            "com.example.app",
            "Example",
            custom.Id,
            true,
            DateTimeOffset.UtcNow));

        await database.Repository.DeleteSchemaAsync(custom.Id);
        var selection = await database.Repository.GetEnabledSelectionAsync("com.example.app");

        Assert.NotNull(selection);
        Assert.Equal(NotificationRepository.DefaultSchemaId, selection.SchemaId);
    }

    private static StoredNotification CreateNotification(DateTimeOffset now) => new(
        Guid.NewGuid().ToString(),
        "com.example.app",
        "Example",
        "Title",
        "Body",
        now,
        NotificationRepository.DefaultSchemaId,
        "Default",
        "{\"type\":\"object\"}",
        NotificationState.Pending,
        0,
        now,
        null,
        null,
        now);

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"notify-classifier-{Guid.NewGuid():N}.db3");

        public TestDatabase()
        {
            Repository = new NotificationRepository(_path);
        }

        public NotificationRepository Repository { get; }

        public void Dispose()
        {
            Repository.Dispose();
            File.Delete(_path);
        }
    }
}
