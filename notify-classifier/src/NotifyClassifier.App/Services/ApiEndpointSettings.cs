namespace NotifyClassifier.App.Services;

public sealed class ApiEndpointSettings
{
    private const string PreferenceKey = "classifier_api_url";
    private const string DefaultUrl = "http://10.0.2.2:5080";
    private readonly string _preferenceKey = PreferenceKey;

    public string Url
    {
        get => Preferences.Default.Get(_preferenceKey, DefaultUrl);
        set => Preferences.Default.Set(_preferenceKey, Normalize(value));
    }

    public Uri GetBaseAddress() => new(Url, UriKind.Absolute);

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter an http or https URL.", nameof(value));
        }

        return uri.ToString().TrimEnd('/');
    }
}
