namespace TennisWatch;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Microsoft.Maui.Handlers.ImageButtonHandler.Mapper.AppendToMapping("PlainWatchControl", (handler, _) =>
            handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping("FitWatchScore", (handler, label) =>
        {
            handler.PlatformView.SetIncludeFontPadding(false);
            handler.PlatformView.FontFeatureSettings = "tnum";
            if (label is Label { AutomationId: "left-points" or "right-points" })
                handler.PlatformView.SetAutoSizeTextTypeUniformWithConfiguration(24, Scoring.ScorePage.PointFontSize, 1,
                    (int)Android.Util.ComplexUnitType.Sp);
            else if (label is Label { AutomationId: "match-number" or "match-date" } metadata)
                handler.PlatformView.SetAutoSizeTextTypeUniformWithConfiguration(9, (int)metadata.FontSize, 1,
                    (int)Android.Util.ComplexUnitType.Sp);
        });
        return MauiApp.CreateBuilder().UseMauiApp<App>().Build();
    }
}
