using PodBridge.Core.Models;

namespace PodBridge.Core.Popup;

/// <summary>
/// What the connect pop-up displays: the model name plus one <see cref="PopupBattery"/>
/// per component the model actually reports. The per-model shape comes from
/// <see cref="AirPodsModelInfo"/> (Tier-1 gates on the model axis only), so a
/// single-unit model shows one gauge and a model without a battery-reporting case
/// hides the case gauge.
/// </summary>
public sealed record PopupContent
{
    /// <summary>The model's human-readable name, e.g. "AirPods Pro".</summary>
    public required string Title { get; init; }

    /// <summary>The gauges to draw, in display order.</summary>
    public required IReadOnlyList<PopupBattery> Batteries { get; init; }

    /// <summary>Builds the pop-up content for a live <paramref name="state"/> of <paramref name="model"/>.</summary>
    /// <param name="state">The live device state.</param>
    /// <param name="model">The per-model shape resolved from <see cref="DeviceState.Model"/>.</param>
    /// <returns>The content to display, titled with the model's name.</returns>
    public static PopupContent ForState(DeviceState state, AirPodsModelInfo model)
        => ForState(state, model, title: null);

    /// <summary>
    /// Builds the pop-up content like <see cref="ForState(DeviceState, AirPodsModelInfo)"/>, but
    /// with a caller-chosen title (for example the owner's own device name).
    /// </summary>
    /// <param name="state">The live device state.</param>
    /// <param name="model">The per-model shape resolved from <see cref="DeviceState.Model"/>.</param>
    /// <param name="title">
    /// The title to show; <see langword="null"/>, empty or whitespace falls back to the
    /// model's <see cref="AirPodsModelInfo.DisplayName"/>.
    /// </param>
    /// <returns>The content to display.</returns>
    public static PopupContent ForState(DeviceState state, AirPodsModelInfo model, string? title)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(model);

        var batteries = new List<PopupBattery>();
        if (model.HasDualBuds)
        {
            batteries.Add(new PopupBattery("Left", state.LeftBatteryPercent, state.LeftCharging));
            batteries.Add(new PopupBattery("Right", state.RightBatteryPercent, state.RightCharging));
        }
        else
        {
            batteries.Add(SingleUnit(state));
        }

        if (model.HasBatteryCase)
        {
            batteries.Add(new PopupBattery("Case", state.CaseBatteryPercent, state.CaseCharging));
        }

        var effectiveTitle = string.IsNullOrWhiteSpace(title) ? model.DisplayName : title;
        return new PopupContent { Title = effectiveTitle, Batteries = batteries };
    }

    // A single-unit model has one battery, but which advertisement slot carries it is not
    // something this code assumes: prefer the left slot and fall back to the right one, so a
    // known value is never hidden behind an unknown sentinel.
    private static PopupBattery SingleUnit(DeviceState state)
        => state.LeftBatteryPercent is not null
            ? new PopupBattery("Battery", state.LeftBatteryPercent, state.LeftCharging)
            : new PopupBattery("Battery", state.RightBatteryPercent, state.RightCharging);
}
