using Microsoft.Extensions.Options;

using NotifyClassifier.Api;
using NotifyClassifier.Core;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.Configure<ClassifierOptions>(builder.Configuration.GetSection(ClassifierOptions.SectionName));
builder.Services.AddSingleton<CodexCliNotificationClassifier>();
builder.Services.AddSingleton<MockNotificationClassifier>();
builder.Services.AddSingleton<INotificationClassifier>(services =>
{
    var options = services.GetRequiredService<IOptions<ClassifierOptions>>().Value;
    return string.Equals(options.Mode, "Mock", StringComparison.OrdinalIgnoreCase)
        ? services.GetRequiredService<MockNotificationClassifier>()
        : services.GetRequiredService<CodexCliNotificationClassifier>();
});

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();

app.MapGet("/", (IOptions<ClassifierOptions> options) => Results.Ok(new
{
    service = "Notify Classifier API",
    model = options.Value.Model,
    mode = options.Value.Mode
}));

app.MapPost("/classify", async (
    ClassificationRequest request,
    INotificationClassifier classifier,
    IOptions<ClassifierOptions> options,
    CancellationToken cancellationToken) =>
{
    var validation = SchemaTools.ValidateDefinition(request.Schema.Definition.GetRawText());
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["schema.definition"] = [validation.Error ?? "The schema is invalid."]
        });
    }

    try
    {
        var result = await classifier.ClassifyAsync(request, cancellationToken);
        return Results.Ok(new ClassificationResponse(
            request.Notification.Id,
            request.Schema.Id,
            request.Schema.Name,
            options.Value.Model,
            result));
    }
    catch (ClassifierUnavailableException exception)
    {
        return Results.Problem(
            title: "Classifier unavailable",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public partial class Program;
