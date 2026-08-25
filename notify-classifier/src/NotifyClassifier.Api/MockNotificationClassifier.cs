using System.Text.Json;

using NotifyClassifier.Core;

namespace NotifyClassifier.Api;

internal sealed class MockNotificationClassifier : INotificationClassifier
{
    public Task<JsonElement> ClassifyAsync(ClassificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SchemaTools.CreateDeterministicExample(request.Schema.Definition));
    }
}
