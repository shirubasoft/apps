using SkiaSharp.Views.Maui.Controls.Hosting;

namespace PenSketch;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder().UseMauiApp<App>().UseSkiaSharp().Build();
}
