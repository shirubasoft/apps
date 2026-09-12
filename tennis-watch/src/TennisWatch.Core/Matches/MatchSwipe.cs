namespace TennisWatch.Core.Matches;

public enum SwipeAction { None, NextMatch, PreviousMatch, DeleteMatch }
public enum SwipeAxis { None, Horizontal, Vertical }

public static class MatchSwipe
{
    public const float TouchSlop = 12;

    public static SwipeAxis Axis(float deltaX, float deltaY)
    {
        if (Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) <= TouchSlop) return SwipeAxis.None;
        if (Math.Abs(deltaY) <= Math.Abs(deltaX) * 0.6f) return SwipeAxis.Horizontal;
        if (Math.Abs(deltaX) <= Math.Abs(deltaY) * 0.6f) return SwipeAxis.Vertical;
        return SwipeAxis.None;
    }

    public static float DeleteDistance(float height) => Math.Max(96, height * 0.45f);

    public static SwipeAction Recognize(SwipeAxis axis, float deltaX, float deltaY, float height)
    {
        if (axis != Axis(deltaX, deltaY)) return SwipeAction.None;
        return axis switch
        {
            SwipeAxis.Horizontal when Math.Abs(deltaX) >= 48 =>
                deltaX < 0 ? SwipeAction.NextMatch : SwipeAction.PreviousMatch,
            SwipeAxis.Vertical when -deltaY >= DeleteDistance(height) => SwipeAction.DeleteMatch,
            _ => SwipeAction.None
        };
    }
}
