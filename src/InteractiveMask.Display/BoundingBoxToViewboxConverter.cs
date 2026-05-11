using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InteractiveMask.Display;

/// <summary>
/// Converts a <see cref="InteractiveMask.Detection.BoundingBox"/>'s four int fields
/// (X, Y, Width, Height) into a single <see cref="Rect"/> for use as
/// <c>ImageBrush.Viewbox</c>. Lets the per-detection overlay sample exactly the
/// detection region from the underlying tile bitmap so a <c>BlurEffect</c> on top
/// produces a real per-region privacy blur rather than just a coloured rectangle.
/// </summary>
public sealed class BoundingBoxToViewboxConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 4) return Rect.Empty;
        // Bindings can be UnsetValue (during template instantiation) or null
        // (if a Box field hasn't been resolved yet). Treat any non-int as zero.
        int x = values[0] is int xi ? xi : 0;
        int y = values[1] is int yi ? yi : 0;
        int w = values[2] is int wi ? wi : 0;
        int h = values[3] is int hi ? hi : 0;
        if (w <= 0 || h <= 0) return Rect.Empty;
        return new Rect(x, y, w, h);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
