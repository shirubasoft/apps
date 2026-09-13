namespace TennisWatch.Core.Matches;

public sealed class MatchDeleteHold
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(1);
    private TimeSpan? startedAt;

    public bool IsConfirmed { get; private set; }
    public bool IsWaiting => startedAt.HasValue && !IsConfirmed;

    public void Update(bool inDeleteZone, TimeSpan now)
    {
        if (IsConfirmed) return;
        startedAt = inDeleteZone ? startedAt ?? now : null;
    }

    public bool TryConfirm(TimeSpan now)
    {
        if (IsConfirmed || startedAt is not { } start || now - start < Duration) return false;
        IsConfirmed = true;
        return true;
    }

    public void Cancel() => startedAt = null;

    public void Reset()
    {
        Cancel();
        IsConfirmed = false;
    }
}
