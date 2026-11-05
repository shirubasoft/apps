using QuietNight.Audio;
using QuietNight.Core.Analysis;
using QuietNight.Night;
using QuietNight.Training;

namespace QuietNight.Masking;

/// <summary>
/// Plays the bedtime masker for a short while before the night, so the user can set its volume by ear, with one rise in
/// the middle like the one a cat's call would bring, so they know what to expect. Like the night itself it stays silent
/// until Android confirms every layer reaches earbuds or headphones, and stops otherwise.
/// </summary>
public sealed class MaskerPreview : IDisposable
{
    public static readonly TimeSpan Length = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RiseAt = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan RiseFor = TimeSpan.FromSeconds(3);

    /// <summary>How far each band rises in the sample rise, shaped like a cat's call in the middle and high bands.</summary>
    public static readonly float[] RiseShapeDb = [0, 0, 2, 8, 12, 9, 3];

    private readonly MaskerPlayer player;
    private readonly float[] levels = new float[MaskerBands.Count];
    private readonly CancellationTokenSource stop = new();
    private NightSettings settings = NightSettings.Default;
    private DateTimeOffset started;

    private MaskerPreview(float[][] layers) => player = new MaskerPlayer(layers);

    /// <summary>Raised on the main thread once the preview has ended, by itself or because it was stopped.</summary>
    public event Action? Ended;

    /// <summary>How long is left to play.</summary>
    public TimeSpan Left => Length - (DateTimeOffset.Now - started);

    /// <summary>Whether the sample rise is playing now.</summary>
    public bool Rising => DateTimeOffset.Now - started is var at && at >= RiseAt && at < RiseAt + RiseFor;

    /// <summary>Starts a preview at the given settings, or returns null when no earbuds or headphones are connected.</summary>
    public static async Task<MaskerPreview?> StartAsync(NightSettings settings)
    {
        if (!AudioRoutes.HeadphonesConnected(Platform.AppContext)) return null;
        var preview = new MaskerPreview(await Task.Run(ModelStore.Layers));
        preview.started = DateTimeOffset.Now;
        preview.SetVolume(settings);
        preview.player.Muted = true;
        preview.player.Start();
        _ = preview.RunAsync();
        return preview;
    }

    public void SetVolume(NightSettings settings)
    {
        this.settings = settings;
        Apply();
    }

    private void Apply()
    {
        settings.BedtimeLevels(levels);
        if (Rising)
            for (var b = 0; b < levels.Length; b++)
                levels[b] += MathF.Min(RiseShapeDb[b], settings.MaxBoostDb);
        player.SetLevels(levels);
    }

    public void Stop() => stop.Cancel();

    /// <summary>Ends the preview; the player is released once its loop sees the stop.</summary>
    public void Dispose() => stop.Cancel();

    private async Task RunAsync()
    {
        var until = started + Length;
        try
        {
            while (DateTimeOffset.Now < until)
            {
                var routed = player.RoutedToEars();
                if (routed == false) break;
                player.Muted = routed != true;
                Apply();
                await Task.Delay(100, stop.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        player.Dispose();
        MainThread.BeginInvokeOnMainThread(() => Ended?.Invoke());
    }
}
