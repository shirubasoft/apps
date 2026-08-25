using NotifyClassifier.Core;
using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Pages;

public sealed class SchemaEditorPage : ContentPage
{
    private readonly INotificationRepository _repository;
    private readonly StoredSchema? _existing;
    private readonly Entry _name;
    private readonly Editor _schema;
    private readonly Switch _isDefault;

    public SchemaEditorPage(INotificationRepository repository, StoredSchema? existing)
    {
        _repository = repository;
        _existing = existing;
        Title = existing is null ? "Create schema" : "Edit schema";
        _name = new Entry
        {
            Text = existing?.Name,
            Placeholder = "Schema name",
            AutomationId = "SchemaNameEntry"
        };
        _schema = new Editor
        {
            Text = existing?.JsonSchema ?? NewSchema,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 360,
            FontFamily = "monospace",
            AutomationId = "SchemaJsonEditor"
        };
        _isDefault = new Switch
        {
            IsToggled = existing?.IsDefault == true,
            AutomationId = "DefaultSchemaSwitch"
        };
        var save = PageStyles.PrimaryButton("Save schema", "SaveSchemaButton");
        save.Clicked += SaveClicked;

        var defaultRow = new HorizontalStackLayout { Spacing = 10 };
        defaultRow.Children.Add(_isDefault);
        defaultRow.Children.Add(new Label { Text = "Use as default", VerticalTextAlignment = TextAlignment.Center });

        var content = PageStyles.Content();
        content.Children.Add(PageStyles.Title(Title));
        content.Children.Add(_name);
        content.Children.Add(defaultRow);
        content.Children.Add(_schema);
        content.Children.Add(save);
        Content = new ScrollView { Content = content };
    }

    private async void SaveClicked(object? sender, EventArgs eventArgs)
    {
        var name = _name.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlertAsync("Name required", "Give this schema a name.", "OK");
            return;
        }

        var json = _schema.Text ?? string.Empty;
        var validation = SchemaTools.ValidateDefinition(json);
        if (!validation.IsValid)
        {
            await DisplayAlertAsync("Invalid JSON Schema", validation.Error ?? "The schema is invalid.", "OK");
            return;
        }

        var updated = new StoredSchema(
            _existing?.Id ?? Guid.NewGuid().ToString(),
            name,
            json,
            _isDefault.IsToggled,
            (_existing?.Version ?? 0) + 1,
            DateTimeOffset.UtcNow);
        await _repository.SaveSchemaAsync(updated);
        await Navigation.PopAsync();
    }

    private const string NewSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "label": { "type": "string" },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
          },
          "required": ["label", "confidence"],
          "additionalProperties": false
        }
        """;
}
