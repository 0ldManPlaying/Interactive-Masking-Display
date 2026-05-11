using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using InteractiveMask.Detection;

namespace InteractiveMask.Display;

/// <summary>
/// Converts an <see cref="ObjectClass"/> value into a <see cref="SolidColorBrush"/>
/// for the per-detection mask fill. Color-codes detections so the operator can
/// scan a video grid and see at a glance which categories are being masked
/// (red = person, blue = vehicle, orange = two-wheeler). Combined with the
/// segmentation OpacityMask + BlurEffect, the result is a coloured silhouette
/// of the detected object that fully obscures the underlying camera content.
/// <para>
/// Alpha is set to ~75% so the soft blur edges fade gracefully into the
/// underlying frame without a hard cut-off line at the mask boundary.
/// </para>
/// </summary>
public sealed class ObjectClassToBrushConverter : IValueConverter
{
    // Cached frozen brushes so each detection doesn't re-allocate.
    private static readonly SolidColorBrush PersonBrush     = Freeze(Color.FromArgb(0xC8, 0xE7, 0x4C, 0x3C)); // crimson red
    private static readonly SolidColorBrush TwoWheelerBrush = Freeze(Color.FromArgb(0xC8, 0xF3, 0x9C, 0x12)); // orange
    private static readonly SolidColorBrush VehicleBrush    = Freeze(Color.FromArgb(0xC8, 0x34, 0x98, 0xDB)); // sky blue
    private static readonly SolidColorBrush FaceBrush       = Freeze(Color.FromArgb(0xC8, 0x9B, 0x59, 0xB6)); // purple - face reserved for v2.x
    private static readonly SolidColorBrush PlateBrush      = Freeze(Color.FromArgb(0xC8, 0x2E, 0xCC, 0x71)); // green - LicensePlate reserved for v2.0.x
    private static readonly SolidColorBrush UnknownBrush    = Freeze(Color.FromArgb(0x80, 0x80, 0x80, 0x80)); // muted grey

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ObjectClass cls) return UnknownBrush;
        return cls switch
        {
            ObjectClass.Person       => PersonBrush,
            ObjectClass.TwoWheeler   => TwoWheelerBrush,
            ObjectClass.Vehicle      => VehicleBrush,
            ObjectClass.Face         => FaceBrush,
            ObjectClass.LicensePlate => PlateBrush,
            _                        => UnknownBrush,
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
