using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PodBridge.App;

/// <summary>
/// Turns a 0–100 battery percentage into the <c>StrokeDashArray</c> that draws that
/// fraction of a circular gauge on the connect pop-up. WPF measures dash lengths in
/// multiples of the stroke thickness and insets an <c>Ellipse</c> by half its stroke, so the
/// ring's centre-line circumference is worked out from the diameter and thickness below
/// (they must match the ring in <c>PopupWindow.xaml</c>). Round caps add half a thickness
/// at each end of a dash, which is subtracted so a full ring closes exactly.
/// </summary>
[ValueConversion(typeof(double), typeof(DoubleCollection))]
public sealed class RingDashConverter : IValueConverter
{
    /// <summary>Outer diameter of the ring, in device-independent pixels.</summary>
    public const double RingDiameter = 72;

    /// <summary>Stroke thickness of the ring, in device-independent pixels.</summary>
    public const double RingThickness = 7;

    private const double CapAllowance = 1; // two round caps, half a thickness each

    private static readonly double CircumferenceInThicknesses
        = Math.PI * (RingDiameter - RingThickness) / RingThickness;

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value is double d ? Math.Clamp(d, 0, 100) : 0;
        var filled = Math.Max(0, (percent / 100 * CircumferenceInThicknesses) - CapAllowance);

        // The gap is longer than the whole ring, so exactly one dash is drawn.
        return new DoubleCollection { filled, CircumferenceInThicknesses + 2 };
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
