#nullable enable
using GDK;
using System.Runtime.InteropServices;

namespace InteractiveMask.Gdk;

/// <summary>
/// Bound camera within an <see cref="NvrSession"/>: one camera index, one stream id.
/// Owns the YV12 + RGB32 decode buffers for this tile and raises <see cref="FrameDecoded"/>
/// on the GDK callback thread. The actual decode call is dispatched by the parent
/// <see cref="NvrSession"/> using the shared <c>g2decoder</c> instance.
/// </summary>
public sealed class CameraTile : IDisposable
{
    private readonly object _bufferLock = new();
    private IntPtr _yv12Buffer;
    private int _yv12BufferLen;
    private IntPtr _rgbBuffer;
    private int _rgbBufferLen;
    private bool _disposed;

    public int CameraIndex { get; }
    public int StreamId { get; private set; }
    public string Label { get; private set; }

    public TileStatus Status { get; private set; } = TileStatus.Pending;
    public string? LastError { get; private set; }

    public event Action<DecodedFrame>? FrameDecoded;
    public event Action<TileStatus, string?>? StatusChanged;

    internal CameraTile(int cameraIndex, int streamId, string label)
    {
        CameraIndex = cameraIndex;
        StreamId = streamId;
        Label = label;
    }

    /// <summary>
    /// Live-apply a stream / label change without disposing the tile. Called by
    /// <see cref="NvrSession.UpdateCameras"/> when Setup saves a config that
    /// changes properties on a camera that already exists.
    /// </summary>
    internal void UpdateStreamAndLabel(int streamId, string label)
    {
        StreamId = streamId;
        Label = label;
    }

    /// <summary>
    /// Called by the NvrSession when an encoded frame for this camera arrives. Decodes
    /// to YV12 (under <paramref name="decoderLock"/>, since the decoder is not thread-safe),
    /// scales to BGRA in our private buffer, and raises <see cref="FrameDecoded"/>.
    ///
    /// The entire decode path runs under <see cref="_bufferLock"/> so that an in-flight
    /// frame and a concurrent <see cref="Dispose"/> are mutually exclusive — without
    /// that guarantee, a late frame for a removed camera could write to memory that
    /// Dispose has already returned to the heap.
    /// </summary>
    internal void HandleFrame(g2decoder decoder, object decoderLock, ref G2FRAME frame)
    {
        int width = frame._width;
        int height = frame._height;
        if (width <= 0 || height <= 0) return;

        int yv12Len = g2decoder.picture_buf_len(width, height, (int)G2DECODER_VIDEO_PIX_FORMAT.TYPE.YV12);
        int rgbLen = g2decoder.picture_buf_len(width, height, (int)G2DECODER_VIDEO_PIX_FORMAT.TYPE.RGB32);

        lock (_bufferLock)
        {
            if (_disposed) return;
            EnsureBufferLocked(ref _yv12Buffer, ref _yv12BufferLen, yv12Len);
            EnsureBufferLocked(ref _rgbBuffer, ref _rgbBufferLen, rgbLen);

            var param = new G2DECODER_VIDEO_PARAM_V3
            {
                _frame = frame,
                _buf = _yv12Buffer,
                _buf_len = (uint)_yv12BufferLen,
                _decoder_id = CameraIndex,
                _threads = 1,
            };

            G2DECODER_VIDEO_RESULT_V3 result;
            bool decompressed;
            lock (decoderLock)
            {
                decompressed = decoder.decompress(ref param, out result);
            }
            if (!decompressed) return;

            if ((G2DECODER_VIDEO_RESULT_V3.RESULT_TYPE)result._result != G2DECODER_VIDEO_RESULT_V3.RESULT_TYPE.SUCCESS)
            {
                return;
            }

            var dstPic = default(G2PICTURE);
            bool ok = g2decoder.picture_scale(
                ref dstPic,
                (int)G2DECODER_VIDEO_PIX_FORMAT.TYPE.RGB32,
                result._res.cx, result._res.cy,
                _rgbBuffer,
                _rgbBufferLen,
                (int)G2DECODER_VIDEO_SCALER_PARAM.FILTER.BILINEAR,
                ref result._pic,
                (int)G2DECODER_VIDEO_PIX_FORMAT.TYPE.YV12,
                result._res.cx, result._res.cy,
                result._color_space,
                result._color_range);

            if (!ok)
            {
                SetStatus(TileStatus.Error, "picture_scale failed");
                return;
            }

            int stride = dstPic.linesize?[0] ?? (result._res.cx * 4);

            if (Status != TileStatus.Live)
            {
                SetStatus(TileStatus.Live, null);
            }

            // FrameDecoded is raised inside the lock so that consumers see consistent
            // buffer pointers; subscribers (TileViewModel.OnFrameDecoded) marshal to
            // the dispatcher with BeginInvoke and copy via WritePixels, so the lock
            // is released long before the UI render.
            FrameDecoded?.Invoke(new DecodedFrame(
                Buffer: _rgbBuffer,
                Width: result._res.cx,
                Height: result._res.cy,
                Stride: stride,
                Codec: (G2DECODER_CODEC.TYPE)frame._extra._decoder));
        }
    }

    internal void SetStatus(TileStatus status, string? detail)
    {
        if (Status == status && LastError == detail) return;
        Status = status;
        LastError = detail;
        StatusChanged?.Invoke(status, detail);
    }

    public void Dispose()
    {
        // Take the buffer lock BEFORE flipping _disposed. HandleFrame holds this
        // lock for the entire decode path, so we wait here until any in-flight
        // frame finishes — the buffer pointer it's using stays valid.
        lock (_bufferLock)
        {
            if (_disposed) return;
            _disposed = true;
            if (_yv12Buffer != IntPtr.Zero) { Marshal.FreeHGlobal(_yv12Buffer); _yv12Buffer = IntPtr.Zero; _yv12BufferLen = 0; }
            if (_rgbBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_rgbBuffer); _rgbBuffer = IntPtr.Zero; _rgbBufferLen = 0; }
        }
    }

    private static void EnsureBufferLocked(ref IntPtr buf, ref int len, int required)
    {
        if (buf != IntPtr.Zero && len >= required) return;
        if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
        buf = Marshal.AllocHGlobal(required);
        len = required;
    }
}
