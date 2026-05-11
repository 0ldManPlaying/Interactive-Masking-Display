using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveMask.Detection;

namespace InteractiveMask.Display;

/// <summary>
/// Converts a <see cref="SegmentationMask"/> into an <see cref="ImageBrush"/>
/// suitable for use as a Rectangle's <c>OpacityMask</c>. Null input returns
/// null (no opacity mask, the full rectangle is opaque - matches the prior
/// bbox-only rendering path). Non-null builds a frozen BGRA32 BitmapSource
/// where the alpha channel carries the mask, then wraps it in an ImageBrush
/// with <c>Stretch=Fill</c> so it covers the host rectangle exactly.
/// <para>
/// The bitmap is freshly created per call. Each frame produces O(detections)
/// allocations that go to the LOH for typical bbox sizes; if profiling shows
/// GC pressure we can switch to an internal pool of WriteableBitmaps reused
/// across detections.
/// </para>
/// </summary>
public sealed class SegmentationMaskToImageBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SegmentationMask mask) return null;
        if (mask.Width <= 0 || mask.Height <= 0) return null;
        if (mask.AlphaData is null || mask.AlphaData.Length < mask.Width * mask.Height) return null;

        // BGRA32: each pixel is 4 bytes. We set RGB to white and the alpha to
        // the mask byte. OpacityMask sampling uses the alpha channel.
        int w = mask.Width;
        int h = mask.Height;
        var bgra = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            int idx = i * 4;
            bgra[idx] = 255;       // B (irrelevant for OpacityMask)
            bgra[idx + 1] = 255;   // G
            bgra[idx + 2] = 255;   // R
            bgra[idx + 3] = mask.AlphaData[i]; // A
        }

        var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
        src.Freeze();

        var brush = new ImageBrush(src)
        {
            Stretch = Stretch.Fill,
            // Keep the bitmap aligned with the host rectangle's local space;
            // we already sized the mask to the bbox dimensions.
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
        };
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
