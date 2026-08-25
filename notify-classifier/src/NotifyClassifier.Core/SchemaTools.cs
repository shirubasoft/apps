using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NotifyClassifier.Core;

public static class SchemaTools
{
    public static SchemaValidationResult ValidateDefinition(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SchemaValidationResult.Invalid("The schema is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SchemaValidationResult.Invalid("The schema root must be a JSON object.");
            }

            return SchemaValidationResult.Valid;
        }
        catch (JsonException exception)
        {
            return SchemaValidationResult.Invalid(exception.Message);
        }
    }

    public static JsonElement CreateDeterministicExample(JsonElement schema)
    {
        var node = CreateNode(schema);
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonNode CreateNode(JsonElement schema)
    {
        if (TryGetNode(schema, "const", out var constant))
        {
            return constant;
        }

        if (TryGetFirstArrayNode(schema, "enum", out var enumValue))
        {
            return enumValue;
        }

        if (TryGetNode(schema, "default", out var defaultValue))
        {
            return defaultValue;
        }

        if (TryGetFirstSchema(schema, "oneOf", out var oneOf))
        {
            return CreateNode(oneOf);
        }

        if (TryGetFirstSchema(schema, "anyOf", out var anyOf))
        {
            return CreateNode(anyOf);
        }

        return GetType(schema) switch
        {
            "object" => CreateObject(schema),
            "array" => CreateArray(schema),
            "integer" => JsonValue.Create(GetMinimum(schema, true)),
            "number" => JsonValue.Create(GetMinimum(schema, false)),
            "boolean" => JsonValue.Create(false),
            "null" => null!,
            _ => JsonValue.Create(CreateString(schema))
        };
    }

    private static JsonObject CreateObject(JsonElement schema)
    {
        var result = new JsonObject();
        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in properties.EnumerateObject())
        {
            result[property.Name] = CreateNode(property.Value);
        }

        return result;
    }

    private static JsonArray CreateArray(JsonElement schema)
    {
        var result = new JsonArray();
        var count = schema.TryGetProperty("minItems", out var minItems) && minItems.TryGetInt32(out var value)
            ? Math.Max(value, 0)
            : 0;

        if (!schema.TryGetProperty("items", out var items))
        {
            return result;
        }

        for (var index = 0; index < count; index++)
        {
            result.Add(CreateNode(items));
        }

        return result;
    }

    private static decimal GetMinimum(JsonElement schema, bool integer)
    {
        if (schema.TryGetProperty("minimum", out var minimum) && minimum.TryGetDecimal(out var value))
        {
            return integer ? decimal.Ceiling(value) : value;
        }

        return decimal.Zero;
    }

    private static string CreateString(JsonElement schema)
    {
        if (schema.TryGetProperty("format", out var format))
        {
            return format.GetString() switch
            {
                "date-time" => DateTimeOffset.UnixEpoch.ToString("O", CultureInfo.InvariantCulture),
                "date" => "1970-01-01",
                "time" => "00:00:00Z",
                "email" => "notification@example.invalid",
                "uri" => "https://example.invalid/notification",
                "uuid" => Guid.Empty.ToString(),
                _ => "classified"
            };
        }

        return "classified";
    }

    private static string GetType(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return schema.TryGetProperty("properties", out _) ? "object" : "string";
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return type.GetString() ?? "string";
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            return type.EnumerateArray()
                .Select(item => item.GetString())
                .FirstOrDefault(value => value is not null and not "null") ?? "null";
        }

        return "string";
    }

    private static bool TryGetFirstSchema(JsonElement schema, string propertyName, out JsonElement result)
    {
        if (schema.TryGetProperty(propertyName, out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            result = choices[0];
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryGetFirstArrayNode(JsonElement schema, string propertyName, out JsonNode result)
    {
        if (schema.TryGetProperty(propertyName, out var values) &&
            values.ValueKind == JsonValueKind.Array &&
            values.GetArrayLength() > 0)
        {
            result = JsonNode.Parse(values[0].GetRawText())!;
            return true;
        }

        result = null!;
        return false;
    }

    private static bool TryGetNode(JsonElement schema, string propertyName, out JsonNode result)
    {
        if (schema.TryGetProperty(propertyName, out var value))
        {
            result = JsonNode.Parse(value.GetRawText())!;
            return true;
        }

        result = null!;
        return false;
    }
}
