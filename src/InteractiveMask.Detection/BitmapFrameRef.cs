using System;

namespace InteractiveMask.Detection;

/// <summary>
/// A frame held as a contiguous BGRA8 pixel buffer in managed memory. Used by the
/// PoC smoke tests and by Display when wiring a <see cref="System.Windows.Media.Imaging.WriteableBitmap"/>
/// into the detector. The buffer layout matches the WPF Bgra32 pixel format produced
/// by the IDIS GDK decode callback, so the Display side can hand its existing
/// WriteableBitmap.BackBuffer through verbatim.
/// </summary>
public sealed record BitmapFrameRef(
    long TimestampTicks,
    int Width,
    int Height,
    int StreamId,
    byte[] BgraPixels,
    int Stride)
    : FrameRef(TimestampTicks, Width, Height, StreamId)
{
    /// <summary>Convenience constructor; computes Stride as Width * 4.</summary>
    public static BitmapFrameRef FromBgra(long timestampTicks, int width, int height, int streamId, byte[] bgraPixels)
    {
        if (bgraPixels.Length < width * height * 4)
        {
            throw new ArgumentException(
                $"Expected at least {width * height * 4} bytes for {width}x{height} BGRA8, got {bgraPixels.Length}.",
                nameof(bgraPixels));
        }
        return new BitmapFrameRef(timestampTicks, width, height, streamId, bgraPixels, width * 4);
    }
}
