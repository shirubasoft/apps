namespace TennisWatch;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Microsoft.Maui.Handlers.ImageButtonHandler.Mapper.AppendToMapping("PlainWatchControl", (handler, _) =>
            handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping("FitWatchScore", (handler, label) =>
        {
            if (label is Label { AutomationId: "left-points" or "right-points" })
                handler.PlatformView.SetAutoSizeTextTypeUniformWithConfiguration(24, 42, 1,
                    (int)Android.Util.ComplexUnitType.Sp);
        });
        return MauiApp.CreateBuilder().UseMauiApp<App>().Build();
    }
}
