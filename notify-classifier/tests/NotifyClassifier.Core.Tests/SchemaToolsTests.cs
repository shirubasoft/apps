using System.Text.Json;

namespace NotifyClassifier.Core.Tests;

public sealed class SchemaToolsTests
{
    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"type\":\"object\"}", true)]
    [InlineData("[]", false)]
    [InlineData("true", false)]
    [InlineData("{", false)]
    [InlineData("", false)]
    public void ValidateDefinitionChecksForAJsonObject(string json, bool expected)
    {
        Assert.Equal(expected, SchemaTools.ValidateDefinition(json).IsValid);
    }

    [Theory]
    [MemberData(nameof(ExampleCases))]
    public void CreateDeterministicExampleHonorsCommonSchemaKeywords(string schemaJson, string expectedJson)
    {
        using var schema = JsonDocument.Parse(schemaJson);
        using var expected = JsonDocument.Parse(expectedJson);

        var actual = SchemaTools.CreateDeterministicExample(schema.RootElement);

        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual));
    }

    public static TheoryData<string, string> ExampleCases => new()
    {
        {
            """{"type":"object","properties":{"kind":{"enum":["first","second"]},"urgent":{"type":"boolean"}}}""",
            """{"kind":"first","urgent":false}"""
        },
        { """{"type":"array","minItems":2,"items":{"type":"integer","minimum":3}}""", "[3,3]" },
        { """{"oneOf":[{"const":"chosen"},{"const":"ignored"}]}""", "\"chosen\"" },
        { """{"type":"string","format":"date"}""", "\"1970-01-01\"" },
        { """{"type":"string","format":"date-time"}""", "\"1970-01-01T00:00:00.0000000+00:00\"" },
        { """{"type":"string","format":"time"}""", "\"00:00:00Z\"" },
        { """{"type":"string","format":"email"}""", "\"notification@example.invalid\"" },
        { """{"type":"string","format":"uri"}""", "\"https://example.invalid/notification\"" },
        { """{"type":"string","format":"uuid"}""", "\"00000000-0000-0000-0000-000000000000\"" },
        { """{"type":"string","format":"unknown"}""", "\"classified\"" },
        { """{"type":"string"}""", "\"classified\"" },
        { """{"type":["null","boolean"]}""", "false" },
        { """{"type":"null"}""", "null" },
        { """{"properties":{}}""", "{}" },
        { """{"type":"number","minimum":1.5}""", "1.5" },
        { """{"type":"integer","minimum":1.5}""", "2" },
        { """{"default":{"source":"default"}}""", "{\"source\":\"default\"}" },
        { """{"anyOf":[{"const":7},{"const":8}]}""", "7" }
    };
}
