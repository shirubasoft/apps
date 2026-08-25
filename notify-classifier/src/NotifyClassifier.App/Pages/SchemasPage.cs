using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Pages;

public sealed class SchemasPage(INotificationRepository repository) : ContentPage
{
    private readonly VerticalStackLayout _rows = new() { Spacing = 8 };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Content is not null)
        {
            return;
        }

        Title = "Schemas";
        var add = PageStyles.PrimaryButton("Create schema", "CreateSchemaButton");
        add.Clicked += async (_, _) => await Navigation.PushAsync(new SchemaEditorPage(repository, null));
        var content = PageStyles.Content();
        content.Children.Add(PageStyles.Title("JSON schemas"));
        content.Children.Add(PageStyles.Subtitle("Create strict output shapes and assign them to monitored apps."));
        content.Children.Add(add);
        content.Children.Add(_rows);
        Content = new ScrollView { Content = content };
    }

    private async Task RefreshAsync()
    {
        await repository.InitializeAsync();
        var schemas = await repository.GetSchemasAsync();
        _rows.Children.Clear();
        foreach (var schema in schemas)
        {
            _rows.Children.Add(CreateRow(schema));
        }
    }

    private VerticalStackLayout CreateRow(StoredSchema schema)
    {
        var edit = new Button { Text = "Edit", AutomationId = $"EditSchema_{schema.Id}" };
        edit.Clicked += async (_, _) => await Navigation.PushAsync(new SchemaEditorPage(repository, schema));
        var remove = new Button
        {
            Text = "Delete",
            IsEnabled = !schema.IsDefault,
            AutomationId = $"DeleteSchema_{schema.Id}"
        };
        remove.Clicked += async (_, _) =>
        {
            var confirmed = await DisplayAlertAsync("Delete schema?", schema.Name, "Delete", "Cancel");
            if (confirmed)
            {
                await repository.DeleteSchemaAsync(schema.Id);
                await RefreshAsync();
            }
        };

        var header = new HorizontalStackLayout { Spacing = 8 };
        header.Children.Add(edit);
        header.Children.Add(remove);
        var layout = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(4, 8) };
        layout.Children.Add(new Label
        {
            Text = schema.IsDefault ? $"{schema.Name}  •  default" : schema.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 17
        });
        layout.Children.Add(new Label { Text = $"Version {schema.Version}", FontSize = 12 });
        layout.Children.Add(header);
        layout.Children.Add(PageStyles.Divider());
        return layout;
    }
}
