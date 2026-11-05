using QuietNight.Core.Analysis;
using QuietNight.Core.Masking;

namespace QuietNight.Ui;

/// <summary>
/// The masker drawn as seven soft columns, one per band from low to high pitch. Each stands at the masker's bedtime
/// shape and grows in lamplight as its band is boosted, so the user sees the noise rise only where a sound needs it.
/// </summary>
public sealed class BandSkyline : GraphicsView, IDrawable
{
    private const float Ease = 0.35f;

    private readonly float[] shown = new float[MaskerBands.Count];
    private readonly float[] target = new float[MaskerBands.Count];
    private readonly IDispatcherTimer frames;
    private float maxBoostDb = 30;
    private bool awake;

    public BandSkyline()
    {
        Drawable = this;
        HeightRequest = 120;
        InputTransparent = true;
        frames = Dispatcher.CreateTimer();
        frames.Interval = TimeSpan.FromMilliseconds(33);
        frames.Tick += (_, _) => Step();
        SemanticProperties.SetDescription(this, Describe());
    }

    /// <summary>Whether the night is running; the columns glow brighter while it is.</summary>
    public bool Awake
    {
        get => awake;
        set
        {
            if (awake == value) return;
            awake = value;
            Invalidate();
        }
    }

    /// <summary>Moves the columns toward the given boosts.</summary>
    public void Show(IReadOnlyList<float> boostDb, float maxBoostDb)
    {
        this.maxBoostDb = MathF.Max(1, maxBoostDb);
        var changed = false;
        for (var b = 0; b < target.Length; b++)
        {
            var next = b < boostDb.Count ? Math.Clamp(boostDb[b], 0, this.maxBoostDb) : 0;
            changed |= Math.Abs(next - target[b]) > 0.05f;
            target[b] = next;
        }
        if (!changed) return;
        SemanticProperties.SetDescription(this, Describe());
        Animate();
    }

    /// <summary>Stops animating, for when the page is hidden.</summary>
    public void Rest() => frames.Stop();

    public void Draw(ICanvas canvas, RectF rect)
    {
        var count = shown.Length;
        var width = MathF.Min(18, rect.Width / (count * 2.6f));
        var gap = (rect.Width - width * count) / (count - 1);
        var floor = rect.Bottom;
        for (var b = 0; b < count; b++)
        {
            var x = rect.Left + b * (width + gap);
            // The bedtime shape tilts down toward high pitch, like the masker itself.
            var baseHeight = rect.Height * (0.3f + 0.025f * (MaskerSettings.ShapeDb[b] + 9));
            var boostHeight = (rect.Height - baseHeight - 4) * Math.Clamp(shown[b] / maxBoostDb, 0, 1);
            var top = floor - baseHeight - boostHeight;
            canvas.FillColor = Theme.Moonlight.WithAlpha(awake ? 0.55f : 0.3f);
            canvas.FillRoundedRectangle(x, floor - baseHeight, width, baseHeight, width / 2);
            if (boostHeight < 1) continue;
            canvas.FillColor = Theme.Lamplight.WithAlpha(awake ? 0.9f : 0.6f);
            canvas.FillRoundedRectangle(x, top, width, boostHeight + width / 2, width / 2);
        }
    }

    private void Animate()
    {
        if (Theme.ReducedMotion)
        {
            target.CopyTo(shown, 0);
            Invalidate();
            return;
        }
        if (!frames.IsRunning) frames.Start();
    }

    private void Step()
    {
        var moving = false;
        for (var b = 0; b < shown.Length; b++)
        {
            var delta = target[b] - shown[b];
            if (Math.Abs(delta) < 0.1f)
                shown[b] = target[b];
            else
            {
                shown[b] += delta * Ease;
                moving = true;
            }
        }
        Invalidate();
        if (!moving) frames.Stop();
    }

    private string Describe()
    {
        var raised = Enumerable.Range(0, target.Length).Where(b => target[b] >= 1).ToList();
        if (raised.Count == 0) return "Masker bands, all at the bedtime level";
        var regions = new List<string>();
        if (raised.Any(b => b <= 2)) regions.Add("low");
        if (raised.Any(b => b is 3 or 4)) regions.Add("middle");
        if (raised.Any(b => b >= 5)) regions.Add("high");
        return $"Masker raised in the {string.Join(" and ", regions)} bands, by up to {target.Max():F0} decibels";
    }
}
