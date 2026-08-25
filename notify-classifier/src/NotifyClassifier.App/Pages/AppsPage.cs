using NotifyClassifier.App.Services;
using NotifyClassifier.Core;
using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Pages;

public sealed class AppsPage : ContentPage
{
    private readonly IInstalledAppSource _appSource;
    private readonly INotificationRepository _repository;
    private readonly VerticalStackLayout _rows = new() { Spacing = 4 };
    private readonly SearchBar _search;
    private int _searchVersion;
    private IReadOnlyList<InstalledApp> _apps = [];
    private IReadOnlyList<StoredSchema> _schemas = [];
    private Dictionary<string, StoredAppSelection> _selections = new(StringComparer.Ordinal);

    public AppsPage(IInstalledAppSource appSource, INotificationRepository repository)
    {
        _appSource = appSource;
        _repository = repository;
        Title = "Apps";
        _search = new SearchBar
        {
            Placeholder = "Search app or package",
            AutomationId = "AppSearch"
        };
        _search.TextChanged += OnSearchTextChanged;

        var content = PageStyles.Content();
        content.Children.Add(PageStyles.Title("Monitored apps"));
        content.Children.Add(PageStyles.Subtitle("Choose the installed apps to watch and the schema used for each one."));
        content.Children.Add(_search);
        content.Children.Add(_rows);
        Content = new ScrollView { Content = content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await _repository.InitializeAsync();
        _schemas = await _repository.GetSchemasAsync();
        _selections = (await _repository.GetSelectionsAsync())
            .ToDictionary(item => item.PackageName, StringComparer.Ordinal);
        _apps = await _appSource.GetInstalledAppsAsync();
        RebuildRows();
    }

    private void RebuildRows()
    {
        _rows.Children.Clear();
        var query = _search.Text?.Trim();
        var matches = _apps.Where(app => Matches(app, query));
        if (string.IsNullOrWhiteSpace(query))
        {
            matches = matches.Take(80);
        }

        foreach (var app in matches)
        {
            _rows.Children.Add(CreateRow(app));
        }

        if (_rows.Children.Count == 0)
        {
            _rows.Children.Add(PageStyles.Subtitle("No installed apps match this search."));
        }
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs args)
    {
        var version = ++_searchVersion;
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        if (version == _searchVersion)
        {
            RebuildRows();
        }
    }

    private VerticalStackLayout CreateRow(InstalledApp app)
    {
        _selections.TryGetValue(app.PackageName, out var selection);
        var selectedSchemaId = selection?.SchemaId ?? _schemas.First(schema => schema.IsDefault).Id;
        var toggle = new Switch
        {
            IsToggled = selection?.IsEnabled == true,
            AutomationId = $"AppToggle_{Sanitize(app.PackageName)}"
        };
        var picker = new Picker
        {
            Title = "Classification schema",
            ItemsSource = _schemas.Select(schema => schema.Name).ToArray(),
            SelectedIndex = Math.Max(0, _schemas.ToList().FindIndex(schema => schema.Id == selectedSchemaId)),
            AutomationId = $"SchemaPicker_{Sanitize(app.PackageName)}"
        };

        async Task SaveAsync()
        {
            var schemaIndex = Math.Max(picker.SelectedIndex, 0);
            var schema = _schemas[schemaIndex];
            var updated = new StoredAppSelection(
                app.PackageName,
                app.DisplayName,
                schema.Id,
                toggle.IsToggled,
                DateTimeOffset.UtcNow);
            await _repository.SaveSelectionAsync(updated);
            _selections[app.PackageName] = updated;
        }

        toggle.Toggled += async (_, _) => await SaveAsync();
        picker.SelectedIndexChanged += async (_, _) => await SaveAsync();

        var labels = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Fill };
        labels.Children.Add(new Label { Text = app.DisplayName, FontAttributes = FontAttributes.Bold });
        labels.Children.Add(new Label { Text = app.PackageName, FontSize = 12, TextColor = Color.FromArgb("#566473") });
        labels.Children.Add(picker);

        var grid = new Grid
        {
            Padding = new Thickness(4, 10),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Add(labels, 0);
        grid.Add(toggle, 1);

        var wrapper = new VerticalStackLayout { Spacing = 0 };
        wrapper.Children.Add(grid);
        wrapper.Children.Add(PageStyles.Divider());
        return wrapper;
    }

    private static bool Matches(InstalledApp app, string? query) =>
        string.IsNullOrWhiteSpace(query) ||
        app.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        app.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string Sanitize(string value) => value.Replace(".", "_", StringComparison.Ordinal);
}
