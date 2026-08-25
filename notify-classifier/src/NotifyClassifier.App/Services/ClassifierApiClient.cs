using System.Text.Json;

using NotifyClassifier.Core;

namespace NotifyClassifier.App.Services;

public sealed class ClassifierApiClient(HttpClient httpClient, ApiEndpointSettings settings)
{
    public async Task<ClassificationResponse> ClassifyAsync(
        ClassificationRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(settings.GetBaseAddress(), "/classify"))
        {
            Content = JsonContent.Create(request)
        };
        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Classifier API returned {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<ClassificationResponse>(cancellationToken) ??
            throw new JsonException("Classifier API returned an empty response.");
    }
}
