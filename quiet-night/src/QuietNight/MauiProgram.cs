using QuietNight.Ui;

namespace QuietNight;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", Theme.Regular);
            fonts.AddFont("OpenSans-Semibold.ttf", Theme.Semibold);
        });
        return builder.Build();
    }
}
