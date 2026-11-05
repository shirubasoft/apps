using QuietNight.Night;

namespace QuietNight.Ui;

/// <summary>How dark the screen keeps during a night, a choice in Fine-tune.</summary>
public enum NightScreen
{
    /// <summary>Low while in use, and at its minimum after half a minute untouched.</summary>
    Darkest,

    /// <summary>Low while the night runs, without dimming further by itself.</summary>
    Low,

    /// <summary>The usual brightness; the Dim button still darkens it on purpose.</summary>
    Normal,
}

/// <summary>How much of the night screen is showing.</summary>
public enum NightView
{
    /// <summary>Everything, at a capped low brightness.</summary>
    Full,

    /// <summary>What a glance needs: the time, the bands and the controls, at a capped low brightness.</summary>
    Glance,

    /// <summary>The glance view at the lowest brightness the screen allows.</summary>
    Dimmed,
}

/// <summary>
/// Keeps the screen dark while a night runs. After half a minute untouched it dims to its minimum. The touch that
/// wakes it only wakes it, to the glance view, so it cannot move a control the user could not see; one more tap shows
/// everything. With a screen reader on, touches always go through, since they move its focus rather than press.
/// When the night ends the screen stays capped until the next touch or until the user leaves, so stopping a night in
/// the dark does not light the room.
/// </summary>
public sealed class Dimmer
{
    public static readonly TimeSpan After = TimeSpan.FromSeconds(30);
    private const float WakeCap = 0.15f;

    /// <summary>The darkest choice wakes dimmer still, since an OLED screen at 15 % lights a dark room.</summary>
    private const float DarkestCap = 0.05f;

    private DateTimeOffset lastTouch = DateTimeOffset.Now;
    private bool night;
    private bool afterNight;

    public Dimmer() => MainActivity.Intercept = Touch;

    /// <summary>How dark the screen keeps during a night.</summary>
    public static NightScreen Screen
    {
        get => (NightScreen)Preferences.Default.Get("night-screen", (int)NightScreen.Darkest);
        set => Preferences.Default.Set("night-screen", (int)value);
    }

    public NightView View { get; private set; } = NightView.Full;

    /// <summary>Raised when the view changes.</summary>
    public event Action? Changed;

    /// <param name="running">Whether a night is running.</param>
    /// <param name="mayDim">Whether the screen may dim now; false while the user is answering a question.</param>
    public void Update(bool running, bool mayDim = true)
    {
        if (running != night)
        {
            afterNight = night && !running;
            night = running;
            Set(NightView.Full);
        }
        if (running && mayDim && Screen == NightScreen.Darkest && View != NightView.Dimmed && DateTimeOffset.Now - lastTouch >= After)
            Set(NightView.Dimmed);
    }

    /// <summary>Dims at once, for the user who wants the dark now.</summary>
    public void DimNow()
    {
        if (night) Set(NightView.Dimmed);
    }

    /// <summary>Shows everything after a glance.</summary>
    public void ShowAll()
    {
        lastTouch = DateTimeOffset.Now;
        Set(NightView.Full);
    }

    /// <summary>Counts a touch; returns true when it only woke the dimmed screen and should go no further.</summary>
    public bool Touch()
    {
        lastTouch = DateTimeOffset.Now;
        if (afterNight)
        {
            afterNight = false;
            Set(NightView.Full);
        }
        if (View != NightView.Dimmed) return false;
        Set(NightView.Glance);
        return !Theme.ScreenReader;
    }

    /// <summary>
    /// Shows everything as the user leaves the page. During a night the brightness stays capped, since the other tab is
    /// just as bright; otherwise it goes back to the system's.
    /// </summary>
    public void Release()
    {
        afterNight = false;
        night = NightSession.Status.Running || NightSession.Starting;
        Set(NightView.Full);
    }

    private void Set(NightView view)
    {
        var changed = view != View;
        View = view;
        if (Platform.CurrentActivity?.Window is { } window && window.Attributes is { } attributes)
        {
            // -1 hands brightness back to the system setting.
            attributes.ScreenBrightness = view == NightView.Dimmed && night ? 0.01f
                : (night || afterNight) && Screen != NightScreen.Normal ? MathF.Min(SystemBrightness(), Screen == NightScreen.Darkest ? DarkestCap : WakeCap)
                : -1f;
            window.Attributes = attributes;
        }
        if (changed) Changed?.Invoke();
    }

    private static float SystemBrightness()
    {
        var resolver = Platform.AppContext.ContentResolver;
        return resolver is null ? WakeCap : Android.Provider.Settings.System.GetInt(resolver, Android.Provider.Settings.System.ScreenBrightness, 128) / 255f;
    }
}
