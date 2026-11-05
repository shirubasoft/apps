using QuietNight.Ui;

namespace QuietNight;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("AtkinsonNext-Regular.ttf", Theme.Regular);
            fonts.AddFont("AtkinsonNext-SemiBold.ttf", Theme.Semibold);
            fonts.AddFont("Fraunces-Soft.ttf", Theme.Display);
        });
        return builder.Build();
    }
}
