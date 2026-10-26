using PodBridge.Core.Models;
using PodBridge.Core.Popup;
using Xunit;

namespace PodBridge.Core.Tests.Popup;

/// <summary>
/// Device-independent tests (constitution Tier-1 gate) for the pure
/// <see cref="PopupContent"/> / <see cref="PopupBattery"/> mapping that drives the
/// connect pop-up: gauges follow the per-model shape, unknown levels are explicit, and
/// low / charging data is decided in Core rather than in the UI.
/// </summary>
public class PopupContentTests
{
    // Expected values live in static readonly fields (CA1861: no constant array arguments).
    private static readonly string[] BudsAndCaseLabels = ["Left", "Right", "Case"];
    private static readonly string[] BudsAndCasePercentTexts = ["80%", "70%", "100%"];
    private static readonly bool[] BudsAndCaseCharging = [false, false, true];
    private static readonly string[] BudsOnlyLabels = ["Left", "Right"];

    private static AirPodsModelInfo Dual(bool batteryCase = true) => new()
    {
        Model = AirPodsModel.AirPodsPro,
        DisplayName = "AirPods Pro",
        HasDualBuds = true,
        HasBatteryCase = batteryCase,
        HasInEarDetection = true,
    };

    private static AirPodsModelInfo Single() => new()
    {
        Model = AirPodsModel.Unknown,
        DisplayName = "Single unit",
        HasDualBuds = false,
        HasBatteryCase = false,
        HasInEarDetection = false,
    };

    [Fact]
    public void DualBudsWithCase_ShowsLeftRightCaseInOrder()
    {
        var state = new DeviceState
        {
            IsLive = true,
            LeftBatteryPercent = 80,
            RightBatteryPercent = 70,
            CaseBatteryPercent = 100,
            CaseCharging = true,
        };

        var content = PopupContent.ForState(state, Dual());

        Assert.Equal("AirPods Pro", content.Title);
        Assert.Equal(BudsAndCaseLabels, content.Batteries.Select(b => b.Label));
        Assert.Equal(BudsAndCasePercentTexts, content.Batteries.Select(b => b.PercentText));
        Assert.Equal(BudsAndCaseCharging, content.Batteries.Select(b => b.Charging));
    }

    [Fact]
    public void CustomTitle_ReplacesModelName()
    {
        var content = PopupContent.ForState(DeviceState.Unknown, Dual(), "Bryan Chuah's AirPods Pro");

        Assert.Equal("Bryan Chuah's AirPods Pro", content.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankCustomTitle_FallsBackToModelName(string? title)
        => Assert.Equal("AirPods Pro", PopupContent.ForState(DeviceState.Unknown, Dual(), title).Title);

    [Fact]
    public void ModelWithoutBatteryCase_HidesCaseGauge()
    {
        var state = new DeviceState { IsLive = true, LeftBatteryPercent = 50, RightBatteryPercent = 60 };

        var content = PopupContent.ForState(state, Dual(batteryCase: false));

        Assert.Equal(BudsOnlyLabels, content.Batteries.Select(b => b.Label));
    }

    [Fact]
    public void SingleUnit_ShowsOneBatteryGauge()
    {
        var state = new DeviceState { IsLive = true, LeftBatteryPercent = 40, LeftCharging = true };

        var content = PopupContent.ForState(state, Single());

        var battery = Assert.Single(content.Batteries);
        Assert.Equal("Battery", battery.Label);
        Assert.Equal(40, battery.Percent);
        Assert.True(battery.Charging);
    }

    [Fact]
    public void SingleUnit_FallsBackToRightSlotWhenLeftUnknown()
    {
        var state = new DeviceState { IsLive = true, RightBatteryPercent = 90 };

        var battery = Assert.Single(PopupContent.ForState(state, Single()).Batteries);

        Assert.Equal(90, battery.Percent);
    }

    [Fact]
    public void UnknownLevel_RendersUnknownAndEmptyBar()
    {
        var battery = new PopupBattery("Left", Percent: null, Charging: false);

        Assert.Equal(PopupBattery.UnknownText, battery.PercentText);
        Assert.Equal(0d, battery.BarPercent);
        Assert.False(battery.IsLow);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(20, true)]
    [InlineData(30, false)]
    [InlineData(100, false)]
    public void IsLow_FlagsAtOrBelowThreshold(int percent, bool expectedLow)
        => Assert.Equal(expectedLow, new PopupBattery("Left", percent, Charging: false).IsLow);

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PopupContent.ForState(null!, Dual()));
        Assert.Throws<ArgumentNullException>(() => PopupContent.ForState(DeviceState.Unknown, null!));
    }
}
