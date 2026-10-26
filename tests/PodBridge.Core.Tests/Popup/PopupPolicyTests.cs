using Microsoft.Extensions.Time.Testing;
using PodBridge.Core.Models;
using PodBridge.Core.Popup;
using Xunit;

namespace PodBridge.Core.Tests.Popup;

/// <summary>
/// Device-independent tests (constitution Tier-1 gate) for <see cref="PopupPolicy"/>:
/// the pop-up fires on the not-live → live transition only, and a cooldown suppresses
/// repeats when the state flaps. Driven by a fake clock — no physical AirPods.
/// </summary>
public class PopupPolicyTests
{
    private static readonly DeviceState Live = new() { IsLive = true, LeftBatteryPercent = 80 };

    [Fact]
    public void FirstLiveState_Shows()
    {
        var policy = new PopupPolicy(new FakeTimeProvider());

        Assert.True(policy.ShouldShow(Live));
    }

    [Fact]
    public void NotLiveState_DoesNotShow()
    {
        var policy = new PopupPolicy(new FakeTimeProvider());

        Assert.False(policy.ShouldShow(DeviceState.Unknown));
    }

    [Fact]
    public void StillLive_DoesNotShowAgain()
    {
        var policy = new PopupPolicy(new FakeTimeProvider());
        Assert.True(policy.ShouldShow(Live));

        // Battery / in-ear updates keep the state live: no repeat pop-up.
        Assert.False(policy.ShouldShow(Live with { LeftBatteryPercent = 70 }));
        Assert.False(policy.ShouldShow(Live with { LeftInEar = true }));
    }

    [Fact]
    public void LiveAgainAfterCooldown_ShowsAgain()
    {
        var clock = new FakeTimeProvider();
        var policy = new PopupPolicy(clock, TimeSpan.FromSeconds(10));
        Assert.True(policy.ShouldShow(Live));
        Assert.False(policy.ShouldShow(DeviceState.Unknown));

        clock.Advance(TimeSpan.FromSeconds(11));

        Assert.True(policy.ShouldShow(Live));
    }

    [Fact]
    public void LiveAgainWithinCooldown_IsSuppressed()
    {
        var clock = new FakeTimeProvider();
        var policy = new PopupPolicy(clock, TimeSpan.FromSeconds(10));
        Assert.True(policy.ShouldShow(Live));
        Assert.False(policy.ShouldShow(DeviceState.Unknown));

        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.False(policy.ShouldShow(Live));
    }

    [Fact]
    public void SuppressedTransition_StillTracksLiveness()
    {
        // A suppressed live transition must not leave the policy thinking the state is
        // still "not live", or the next genuine transition would be mis-detected.
        var clock = new FakeTimeProvider();
        var policy = new PopupPolicy(clock, TimeSpan.FromSeconds(10));
        Assert.True(policy.ShouldShow(Live));
        Assert.False(policy.ShouldShow(DeviceState.Unknown));
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(policy.ShouldShow(Live)); // suppressed by cooldown

        Assert.False(policy.ShouldShow(Live)); // already live: still no pop-up
    }

    [Fact]
    public void ZeroCooldown_ShowsOnEveryTransition()
    {
        var policy = new PopupPolicy(new FakeTimeProvider(), TimeSpan.Zero);

        Assert.True(policy.ShouldShow(Live));
        Assert.False(policy.ShouldShow(DeviceState.Unknown));
        Assert.True(policy.ShouldShow(Live));
    }

    [Fact]
    public void NegativeCooldown_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new PopupPolicy(new FakeTimeProvider(), TimeSpan.FromSeconds(-1)));

    [Fact]
    public void NullState_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PopupPolicy().ShouldShow(null!));
}
