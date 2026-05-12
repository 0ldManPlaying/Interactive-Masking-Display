using System;
using System.Buffers;

namespace InteractiveMask.Detection;

/// <summary>
/// A frame held as a contiguous BGRA8 pixel buffer in managed memory. Used by the
/// PoC smoke tests and by Display when wiring a <see cref="System.Windows.Media.Imaging.WriteableBitmap"/>
/// into the detector. The buffer layout matches the WPF Bgra32 pixel format produced
/// by the IDIS GDK decode callback, so the Display side can hand its existing
/// WriteableBitmap.BackBuffer through verbatim.
///
/// <para>
/// v2.0.2 memory optimisation: the byte[] backing this frame is in the common case
/// rented from <see cref="ArrayPool{Byte}.Shared"/> rather than freshly allocated
/// (see <see cref="FromPooledBgra"/>). The <see cref="InferenceCoordinator"/> owns
/// the frame after <c>Submit</c> and must call <see cref="Dispose"/> once the
/// inference run (or the slot-replacement drop path) is done with the pixels —
/// <see cref="Dispose"/> returns the array to the pool when it was rented and is
/// a no-op for non-pooled buffers. Holding the array beyond <c>Dispose</c> is a
/// use-after-free and corrupts the next renter.
/// </para>
/// </summary>
public sealed record BitmapFrameRef : FrameRef, IDisposable
{
    public byte[] BgraPixels { get; }
    public int Stride { get; }

    /// <summary>True when <see cref="BgraPixels"/> was rented from
    /// <see cref="ArrayPool{Byte}.Shared"/>. Returns to the pool on
    /// <see cref="Dispose"/>; ignored for caller-owned arrays.</summary>
    public bool IsPooledBgra { get; }

    private int _disposed;

    private BitmapFrameRef(
        long timestampTicks, int width, int height, int streamId,
        byte[] bgraPixels, int stride, bool isPooledBgra)
        : base(timestampTicks, width, height, streamId)
    {
        BgraPixels = bgraPixels;
        Stride = stride;
        IsPooledBgra = isPooledBgra;
    }

    /// <summary>
    /// Build a frame around a caller-owned BGRA buffer. The InferenceCoordinator
    /// will <see cref="Dispose"/> the frame after consumption but Dispose is a
    /// no-op for non-pooled buffers, so the caller's array is unaffected.
    /// </summary>
    public static BitmapFrameRef FromBgra(long timestampTicks, int width, int height, int streamId, byte[] bgraPixels)
    {
        if (bgraPixels.Length < width * height * 4)
        {
            throw new ArgumentException(
                $"Expected at least {width * height * 4} bytes for {width}x{height} BGRA8, got {bgraPixels.Length}.",
                nameof(bgraPixels));
        }
        return new BitmapFrameRef(timestampTicks, width, height, streamId, bgraPixels, width * 4, isPooledBgra: false);
    }

    /// <summary>
    /// Build a frame around a pool-rented BGRA buffer. Caller is expected to
    /// have already filled <paramref name="bgraPixels"/> via <c>Marshal.Copy</c>
    /// or similar from the native frame source. After this call the buffer is
    /// owned by the frame; the InferenceCoordinator returns it to the pool on
    /// <see cref="Dispose"/>. The buffer is allowed to be longer than the
    /// minimum required size (pool rentals often round up).
    /// </summary>
    public static BitmapFrameRef FromPooledBgra(long timestampTicks, int width, int height, int streamId, byte[] bgraPixels)
    {
        int minimumRequired = width * height * 4;
        if (bgraPixels.Length < minimumRequired)
        {
            throw new ArgumentException(
                $"Expected at least {minimumRequired} bytes for {width}x{height} BGRA8, got {bgraPixels.Length}.",
                nameof(bgraPixels));
        }
        return new BitmapFrameRef(timestampTicks, width, height, streamId, bgraPixels, width * 4, isPooledBgra: true);
    }

    /// <summary>
    /// Returns the BGRA buffer to <see cref="ArrayPool{Byte}.Shared"/> when this
    /// frame owns a pool-rented array. Idempotent; safe to call from drop paths,
    /// worker-completion paths and shutdown paths. No-op for non-pooled frames.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (IsPooledBgra)
        {
            // clearArray=false: we don't care about residual pixel data; saves
            // ~Width*Height*4 bytes of memset per frame.
            ArrayPool<byte>.Shared.Return(BgraPixels, clearArray: false);
        }
    }
}
