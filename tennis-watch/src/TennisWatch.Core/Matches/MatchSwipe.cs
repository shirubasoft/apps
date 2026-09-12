namespace TennisWatch.Core.Matches;

public enum SwipeAction { None, NewMatch, PreviousMatch }

public static class MatchSwipe
{
    public static SwipeAction Recognize(float deltaX, float deltaY, float density)
    {
        if (Math.Abs(deltaX) < 48 * density || Math.Abs(deltaY) > Math.Abs(deltaX) * 0.6f)
            return SwipeAction.None;
        return deltaX > 0 ? SwipeAction.NewMatch : SwipeAction.PreviousMatch;
    }
}
