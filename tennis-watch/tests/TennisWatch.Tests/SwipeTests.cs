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
    [InlineData(SwipeAxis.Vertical, 0, -102, 225, SwipeAction.DeleteMatch)]
    [InlineData(SwipeAxis.Vertical, 0, -101, 225, SwipeAction.None)]
    [InlineData(SwipeAxis.Vertical, 0, -96, 192, SwipeAction.DeleteMatch)]
    [InlineData(SwipeAxis.Vertical, 0, -95, 192, SwipeAction.None)]
    [InlineData(SwipeAxis.Vertical, 0, -110, 250, SwipeAction.None)]
    [InlineData(SwipeAxis.Vertical, 0, -113, 250, SwipeAction.DeleteMatch)]
    [InlineData(SwipeAxis.Vertical, 0, 200, 225, SwipeAction.None)]
    [InlineData(SwipeAxis.Vertical, 100, -150, 225, SwipeAction.None)]
    [InlineData(SwipeAxis.Vertical, 80, -150, 225, SwipeAction.DeleteMatch)]
    [InlineData(SwipeAxis.Horizontal, 0, -200, 225, SwipeAction.None)]
    [InlineData(SwipeAxis.None, 0, -200, 225, SwipeAction.None)]
    public void Delete_requires_a_completed_upward_swipe_on_the_locked_axis(
        SwipeAxis axis, float x, float y, float height, SwipeAction expected) =>
        Assert.Equal(expected, MatchSwipe.Recognize(axis, x, y, height));
}
