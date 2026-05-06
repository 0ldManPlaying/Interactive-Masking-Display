using GDK;

namespace InteractiveMask.Gdk;

/// <summary>
/// A single decoded video frame, as produced by <see cref="LiveCamera"/>.
/// The buffer is owned by the camera and reused frame-to-frame; consumers must
/// copy out before the callback returns or read directly while still on the GDK
/// thread.
/// </summary>
/// <param name="Buffer">Pointer to the start of the BGRA pixel data.</param>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="Stride">Bytes per row (typically <c>Width * 4</c> for BGRA).</param>
/// <param name="Codec">Source codec the frame was decoded from (H264 / HEVC).</param>
public readonly record struct DecodedFrame(
    IntPtr Buffer,
    int Width,
    int Height,
    int Stride,
    G2DECODER_CODEC.TYPE Codec);
