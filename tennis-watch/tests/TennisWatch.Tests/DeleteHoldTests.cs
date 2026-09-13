using TennisWatch.Core.Matches;

namespace TennisWatch.Tests;

public sealed class DeleteHoldTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    [InlineData(1500, true)]
    public void Confirmation_requires_a_full_second_in_the_delete_zone(int milliseconds, bool expected)
    {
        var hold = new MatchDeleteHold();
        hold.Update(true, TimeSpan.FromSeconds(5));
        Assert.True(hold.IsWaiting);
        Assert.Equal(expected, hold.TryConfirm(TimeSpan.FromMilliseconds(5000 + milliseconds)));
        Assert.Equal(expected, hold.IsConfirmed);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(900)]
    public void Movement_inside_the_zone_keeps_the_original_deadline(int milliseconds)
    {
        var hold = new MatchDeleteHold();
        hold.Update(true, TimeSpan.Zero);
        hold.Update(true, TimeSpan.FromMilliseconds(milliseconds));
        Assert.True(hold.TryConfirm(TimeSpan.FromSeconds(1)));
        Assert.False(hold.IsWaiting);
        hold.Update(true, TimeSpan.FromSeconds(2));
        Assert.False(hold.TryConfirm(TimeSpan.FromSeconds(4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Leaving_or_canceling_the_zone_requires_a_new_full_hold(bool cancel)
    {
        var hold = new MatchDeleteHold();
        hold.Update(true, TimeSpan.Zero);
        if (cancel) hold.Cancel();
        else hold.Update(false, TimeSpan.FromMilliseconds(900));
        Assert.False(hold.IsWaiting);
        Assert.False(hold.TryConfirm(TimeSpan.FromSeconds(2)));
        hold.Update(true, TimeSpan.FromSeconds(3));
        Assert.False(hold.TryConfirm(TimeSpan.FromMilliseconds(3999)));
        Assert.True(hold.TryConfirm(TimeSpan.FromSeconds(4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_gesture_can_confirm_only_once_until_reset(bool cancelAfterConfirmation)
    {
        var hold = new MatchDeleteHold();
        Assert.False(hold.TryConfirm(TimeSpan.FromSeconds(10)));
        hold.Update(true, TimeSpan.FromSeconds(10));
        Assert.True(hold.TryConfirm(TimeSpan.FromSeconds(11)));
        if (cancelAfterConfirmation) hold.Cancel();
        hold.Update(false, TimeSpan.FromSeconds(12));
        hold.Update(true, TimeSpan.FromSeconds(13));
        Assert.False(hold.TryConfirm(TimeSpan.FromSeconds(20)));
        hold.Reset();
        Assert.False(hold.IsConfirmed);
        Assert.False(hold.TryConfirm(TimeSpan.FromSeconds(21)));
        hold.Update(true, TimeSpan.FromSeconds(21));
        Assert.True(hold.TryConfirm(TimeSpan.FromSeconds(22)));
    }
}
