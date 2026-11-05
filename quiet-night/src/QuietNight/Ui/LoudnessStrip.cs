namespace QuietNight.Ui;

/// <summary>
/// A recording drawn as a strip of bars, one per slice of time, as tall as the loudest moment in it, so the user sees
/// where the noise is, or that there is none, without listening.
/// </summary>
public sealed class LoudnessStrip : GraphicsView, IDrawable
{
    private const int Slices = 60;
    private readonly float[] heights = new float[Slices];
    private float? playhead;

    public LoudnessStrip()
    {
        Drawable = this;
        HeightRequest = 48;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, e) =>
        {
            if (e.GetPosition(this) is { } at && Width > 0) Seek?.Invoke((float)(at.X / Width));
        };
        GestureRecognizers.Add(tap);
    }

    /// <summary>Raised with how far through, from 0 to 1, the user tapped, to play from there.</summary>
    public event Action<float>? Seek;

    /// <summary>How far playback has got, from 0 to 1, drawn as a line across the strip; null when not playing.</summary>
    public float? Playhead
    {
        get => playhead;
        set
        {
            playhead = value;
            Invalidate();
        }
    }

    public void Show(float[] samples)
    {
        Array.Clear(heights);
        var per = Math.Max(1, samples.Length / Slices);
        for (var s = 0; s < Slices; s++)
        {
            var peak = 0f;
            for (var i = s * per; i < Math.Min(samples.Length, (s + 1) * per); i++)
                peak = MathF.Max(peak, MathF.Abs(samples[i]));
            // Loudness on a 60 dB scale, so a quiet meow still shows above silence.
            heights[s] = Math.Clamp((20 * MathF.Log10(peak + 1e-6f) + 60) / 60, 0.04f, 1);
        }
        var loudest = heights.Max();
        var at = Array.IndexOf(heights, loudest) * samples.Length / Slices / Core.Analysis.ListeningFormat.SampleRate;
        SemanticProperties.SetDescription(this, loudest <= 0.1f
            ? "Loudness over the recording: almost silent throughout"
            : $"Loudness over the recording: loudest about {at} seconds in");
        Invalidate();
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (heights.Max() <= 0.1f)
        {
            // An empty take says so, rather than looking like a strip that has not drawn.
            // The words sit in a gap in the line, at the size the user's text setting asks for.
            var size = 14 * (Platform.AppContext.Resources?.Configuration?.FontScale ?? 1);
            var gap = size * 5;
            canvas.StrokeColor = Theme.Outline;
            canvas.StrokeSize = 1;
            canvas.DrawLine(rect.Left, rect.Center.Y, rect.Center.X - gap, rect.Center.Y);
            canvas.DrawLine(rect.Center.X + gap, rect.Center.Y, rect.Right, rect.Center.Y);
            canvas.FontColor = Theme.Haze;
            canvas.FontSize = size;
            canvas.DrawString("Almost silent", rect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }
        var width = rect.Width / Slices;
        canvas.FillColor = Theme.Moonlight;
        for (var s = 0; s < Slices; s++)
        {
            var height = rect.Height * heights[s];
            canvas.FillRoundedRectangle(rect.Left + s * width + width * 0.2f, rect.Center.Y - height / 2, width * 0.6f, height, width * 0.3f);
        }
        if (playhead is not { } at) return;
        canvas.StrokeColor = Theme.Lamplight;
        canvas.StrokeSize = 2;
        var x = rect.Left + rect.Width * Math.Clamp(at, 0, 1);
        canvas.DrawLine(x, rect.Top, x, rect.Bottom);
    }
}
