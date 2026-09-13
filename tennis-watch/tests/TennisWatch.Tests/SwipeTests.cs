using TennisWatch.Core.Matches;

namespace TennisWatch.Tests;

public sealed class SwipeTests
{
    [Theory]
    [InlineData(12, 0, SwipeAxis.None)]
    [InlineData(-13, 0, SwipeAxis.Horizontal)]
    [InlineData(13, 0, SwipeAxis.Horizontal)]
    [InlineData(0, -13, SwipeAxis.Vertical)]
    [InlineData(0, 13, SwipeAxis.Vertical)]
    [InlineData(30, 30, SwipeAxis.None)]
    public void Gesture_axis_requires_clear_direction(float x, float y, SwipeAxis expected) =>
        Assert.Equal(expected, MatchSwipe.Axis(x, y));

    [Theory]
    [InlineData(SwipeAxis.Vertical, 0, -112.5f, 225, true)]
    [InlineData(SwipeAxis.Vertical, 0, -112, 225, false)]
    [InlineData(SwipeAxis.Vertical, 0, -96, 192, true)]
    [InlineData(SwipeAxis.Vertical, 0, -95, 192, false)]
    [InlineData(SwipeAxis.Vertical, 0, -124, 250, false)]
    [InlineData(SwipeAxis.Vertical, 0, -125, 250, true)]
    [InlineData(SwipeAxis.Vertical, 0, 200, 225, false)]
    [InlineData(SwipeAxis.Vertical, 100, -150, 225, false)]
    [InlineData(SwipeAxis.Vertical, 80, -150, 225, true)]
    [InlineData(SwipeAxis.Horizontal, 0, -200, 225, false)]
    [InlineData(SwipeAxis.None, 0, -200, 225, false)]
    public void Hold_starts_only_after_a_deliberate_upward_drag_reaches_the_midpoint(
        SwipeAxis axis, float x, float y, float height, bool expected) =>
        Assert.Equal(expected, MatchSwipe.IsDeleteZone(axis, x, y, height));

    [Theory]
    [InlineData(-120)]
    [InlineData(-300)]
    [InlineData(300)]
    public void Releasing_a_vertical_swipe_never_deletes(float y) =>
        Assert.Equal(SwipeAction.None, MatchSwipe.Recognize(SwipeAxis.Vertical, 0, y));
}
