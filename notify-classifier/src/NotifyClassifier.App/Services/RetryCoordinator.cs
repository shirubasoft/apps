using System.Text.Json;

using NotifyClassifier.Core;
using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Services;

public sealed record RetryRunResult(int Processed, int Completed, int Failed);

public sealed class RetryCoordinator(
    INotificationRepository repository,
    ClassifierApiClient classifierApiClient) : IDisposable
{
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public async Task<RetryRunResult> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            await repository.InitializeAsync(cancellationToken);
            var due = await repository.ClaimDueAsync(DateTimeOffset.UtcNow, 25, cancellationToken);
            var completed = 0;
            var failed = 0;

            foreach (var notification in due)
            {
                try
                {
                    var request = CreateRequest(notification);
                    var response = await classifierApiClient.ClassifyAsync(request, cancellationToken);
                    await repository.MarkCompletedAsync(notification.Id, response.Result.GetRawText(), cancellationToken);
                    completed++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    await repository.MarkFailedAsync(
                        notification.Id,
                        exception.Message,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    failed++;
                }
            }

            return new RetryRunResult(due.Count, completed, failed);
        }
        finally
        {
            _runGate.Release();
        }
    }

    private static ClassificationRequest CreateRequest(StoredNotification notification)
    {
        using var schemaDocument = JsonDocument.Parse(notification.SchemaJson);
        return new ClassificationRequest(
            new NotificationPayload(
                notification.Id,
                notification.PackageName,
                notification.AppName,
                notification.Title,
                notification.Text,
                notification.PostedAt),
            new SchemaPayload(
                notification.SchemaId,
                notification.SchemaName,
                schemaDocument.RootElement.Clone()));
    }

    public void Dispose()
    {
        _runGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
