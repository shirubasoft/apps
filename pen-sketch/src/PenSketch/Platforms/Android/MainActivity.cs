using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Speech;

namespace PenSketch;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private TaskCompletionSource<string?>? speechResult;
    public Task<string?> DictateAsync()
    {
        speechResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraPrompt, "Text for your sketch");
        try { StartActivityForResult(intent, 701); }
        catch (ActivityNotFoundException) { speechResult.TrySetException(new InvalidOperationException("Install or enable a speech recognition app, then try again. You can also use your keyboard microphone.")); }
        return speechResult.Task;
    }
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == 701)
        {
            speechResult?.TrySetResult(resultCode == Result.Ok ? data?.GetStringArrayListExtra(RecognizerIntent.ExtraResults)?.FirstOrDefault() : null);
            speechResult = null;
        }
    }
    protected override void OnDestroy()
    {
        speechResult?.TrySetCanceled();
        base.OnDestroy();
    }
}
