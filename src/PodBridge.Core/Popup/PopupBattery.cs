using System.Globalization;

namespace PodBridge.Core.Popup;

/// <summary>
/// One battery gauge on the connect pop-up — a bud, the case, or a single-unit
/// headphone. Pure presentation data with no UI dependency, so the percent text,
/// bar value and low-battery flag are decided (and tested) in Core.
/// </summary>
/// <param name="Label">Short label, e.g. "Left", "Right", "Case" or "Battery".</param>
/// <param name="Percent">Battery %, or <see langword="null"/> when unknown.</param>
/// <param name="Charging">True while this component is charging.</param>
public sealed record PopupBattery(string Label, int? Percent, bool Charging)
{
    /// <summary>Percent at or below which the gauge is flagged as low.</summary>
    public const int LowThresholdPercent = 20;

    /// <summary>Per-component text when the battery level is unknown.</summary>
    public const string UnknownText = "unknown";

    /// <summary>The percentage as text (e.g. "80%"), or <see cref="UnknownText"/>.</summary>
    public string PercentText
        => Percent is { } percent
            ? percent.ToString(CultureInfo.InvariantCulture) + "%"
            : UnknownText;

    /// <summary>The value to draw in a 0–100 bar; an unknown level draws an empty bar.</summary>
    public double BarPercent => Percent ?? 0;

    /// <summary>True when the level is known and at or below <see cref="LowThresholdPercent"/>.</summary>
    public bool IsLow => Percent is <= LowThresholdPercent;
}
