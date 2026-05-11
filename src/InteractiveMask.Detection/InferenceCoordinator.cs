using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace InteractiveMask.Detection;

/// <summary>
/// Single-worker inference coordinator. Owns the one and only
/// <see cref="InferenceSession"/> and runs every <c>Run()</c> call from one
/// background task; per-stream submissions land in slot-replacement registers
/// so newer frames overwrite older pending ones rather than queueing up.
/// <para>
/// M3.5: rewired for YOLO26n-seg (NMS-free, segmentation). Output format
/// changed from anchor-based (1, 84, 8400) to NMS-free top-K (1, 300, 38)
/// plus prototype masks (1, 32, 160, 160). NMS post-processing is now baked
/// into the model, so the C# decoder is much shorter; the mask-combine step
/// (32 coeffs &#x00D7; 32 prototypes) replaces the previous per-class NMS
/// loop in compute cost. Total warm-pass latency on RTX 3090: ~4-5 ms.
/// </para>
/// </summary>
public sealed class InferenceCoordinator : IAsyncDisposable
{
    // YOLO26n-seg model parameters.
    private const int YoloInputSize = 640;
    // Native prototype-mask resolution from the model; the 32 prototypes are
    // each ProtoSize x ProtoSize.
    private const int ProtoSize = 160;
    private const int NumProtoChannels = 32;
    // Each detection row in output0 has 4 bbox + 1 score + 1 class + 32 mask
    // coefficients = 38 floats.
    private const int DetectionRowSize = 4 + 1 + 1 + NumProtoChannels;
    private const int MaxDetections = 300;
    private const float DefaultObjectConfidence = 0.3f;
    private const float MaskBinaryThreshold = 0.5f;

    /// <summary>
    /// COCO class indices that map to our masking categories. Everything else
    /// (airplane, traffic light, fire hydrant, dog, ...) is ignored. The string
    /// is the COCO label retained on <see cref="DetectedObject.RawClassLabel"/>
    /// for audit and support diagnostics.
    /// </summary>
    private static readonly Dictionary<int, (ObjectClass Category, string Label)> CocoMap = new()
    {
        [0] = (ObjectClass.Person, "person"),
        [1] = (ObjectClass.TwoWheeler, "bicycle"),
        [2] = (ObjectClass.Vehicle, "car"),
        [3] = (ObjectClass.TwoWheeler, "motorcycle"),
        [5] = (ObjectClass.Vehicle, "bus"),
        [7] = (ObjectClass.Vehicle, "truck"),
    };

    private readonly InferenceSession _session;
    private readonly int _maxStreams;
    private readonly BitmapFrameRef?[] _slots;
    private readonly Action<DetectionFrame>?[] _callbacks;
    /// <summary>
    /// Wake-up signal coalesced to capacity 1: any number of submissions while
    /// the worker is processing produce at most one queued wake-up, but the
    /// drain loop below still picks up every populated slot before going back
    /// to wait. Drop-write semantics so Submit never blocks the GDK thread.
    /// </summary>
    private readonly Channel<byte> _wakeup = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleWriter = false, SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private int _lastIdx = -1;
    private float _confidenceThreshold = DefaultObjectConfidence;
    private readonly string _detectionOutputName;
    private readonly string _protoOutputName;
    private readonly bool _hasSegmentation;

    public string ExecutionProvider { get; }

    public InferenceCoordinator(string modelPath, int maxStreams = 32)
    {
        if (maxStreams <= 0) throw new ArgumentOutOfRangeException(nameof(maxStreams));
        _maxStreams = maxStreams;
        _slots = new BitmapFrameRef?[maxStreams];
        _callbacks = new Action<DetectionFrame>?[maxStreams];

        var (session, provider) = CreateSession(modelPath);
        _session = session;
        ExecutionProvider = provider;

        // Inspect the model's outputs to figure out which is the detection
        // tensor and which (if any) carries the segmentation prototypes. NMS-
        // free models emit the detection tensor first; seg variants add a
        // (1, 32, 160, 160) prototype tensor as the second output.
        var outputs = _session.OutputMetadata.ToList();
        if (outputs.Count == 0)
            throw new InvalidOperationException($"Model {modelPath} has no outputs.");
        _detectionOutputName = outputs[0].Key;
        _hasSegmentation = outputs.Count >= 2;
        _protoOutputName = _hasSegmentation ? outputs[1].Key : string.Empty;

        // Worker runs on a long-running threadpool thread so it doesn't compete
        // for short-lived worker slots when many GDK callbacks fire.
        _workerTask = Task.Factory.StartNew(
            () => WorkerLoop(_cts.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    public void SetConfidenceThreshold(float threshold)
    {
        _confidenceThreshold = threshold;
    }

    public void Submit(int streamId, BitmapFrameRef frame, Action<DetectionFrame> callback)
    {
        if (frame is null) throw new ArgumentNullException(nameof(frame));
        if (callback is null) throw new ArgumentNullException(nameof(callback));
        if (streamId < 0 || streamId >= _maxStreams) return;
        if (_cts.IsCancellationRequested) return;

        var oldFrame = Interlocked.Exchange(ref _slots[streamId], frame);
        var oldCallback = Interlocked.Exchange(ref _callbacks[streamId], callback);

        if (oldFrame is not null && oldCallback is not null)
        {
            // Drop-path: previous submission for this stream is being replaced
            // before the worker got to it. Empty result completes the awaiting
            // TaskCompletionSource so no caller hangs.
            try
            {
                oldCallback(new DetectionFrame(
                    FrameTimestampTicks: oldFrame.TimestampTicks,
                    StreamId: oldFrame.StreamId,
                    Detections: Array.Empty<DetectedObject>(),
                    Metrics: new DetectorMetrics(0, QueueDepth: 1, GpuUtilizationPercent: null)));
            }
            catch { }
        }

        _wakeup.Writer.TryWrite(0);
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        try
        {
            await foreach (var _ in _wakeup.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                bool didWork;
                do
                {
                    didWork = false;
                    for (int i = 0; i < _maxStreams; i++)
                    {
                        if (ct.IsCancellationRequested) return;
                        int idx = (_lastIdx + 1 + i) % _maxStreams;
                        var frame = Interlocked.Exchange(ref _slots[idx], null);
                        if (frame is null) continue;
                        var callback = Interlocked.Exchange(ref _callbacks[idx], null);
                        _lastIdx = idx;
                        ProcessSlot(frame, callback);
                        didWork = true;
                        break;
                    }
                } while (didWork && !ct.IsCancellationRequested);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception) { /* worker terminating; status events would surface via detector wrapper */ }
    }

    private void ProcessSlot(BitmapFrameRef frame, Action<DetectionFrame>? callback)
    {
        if (callback is null) return;
        try
        {
            var result = RunInference(frame);
            callback(result);
        }
        catch (Exception)
        {
            try
            {
                callback(new DetectionFrame(
                    FrameTimestampTicks: frame.TimestampTicks,
                    StreamId: frame.StreamId,
                    Detections: Array.Empty<DetectedObject>(),
                    Metrics: new DetectorMetrics(0, 0, null)));
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ Inference

    private DetectionFrame RunInference(BitmapFrameRef bitmapFrame)
    {
        var sw = Stopwatch.StartNew();

        var (inputTensor, scaleX, scaleY) = PreprocessForYolo(bitmapFrame);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", inputTensor),
        };

        IReadOnlyList<DetectedObject> detections;
        using (var outputs = _session.Run(inputs))
        {
            detections = DecodeNmsFree(outputs, _confidenceThreshold, scaleX, scaleY,
                bitmapFrame.Width, bitmapFrame.Height);
        }

        sw.Stop();
        return new DetectionFrame(
            FrameTimestampTicks: bitmapFrame.TimestampTicks,
            StreamId: bitmapFrame.StreamId,
            Detections: detections,
            Metrics: new DetectorMetrics(
                InferenceLatencyMs: sw.Elapsed.TotalMilliseconds,
                QueueDepth: 0,
                GpuUtilizationPercent: null));
    }

    private static (DenseTensor<float> Tensor, float ScaleX, float ScaleY) PreprocessForYolo(BitmapFrameRef frame)
    {
        float scaleX = (float)frame.Width / YoloInputSize;
        float scaleY = (float)frame.Height / YoloInputSize;

        const int plane = YoloInputSize * YoloInputSize;
        var buffer = new float[3 * plane];

        var pixels = frame.BgraPixels;
        int stride = frame.Stride;
        int srcH = frame.Height;
        int srcW = frame.Width;

        const float Scale = 1.0f / 255.0f;

        for (int dy = 0; dy < YoloInputSize; dy++)
        {
            int sy = (int)(dy * scaleY);
            if (sy >= srcH) sy = srcH - 1;
            int rowBase = sy * stride;
            int outRow = dy * YoloInputSize;
            for (int dx = 0; dx < YoloInputSize; dx++)
            {
                int sx = (int)(dx * scaleX);
                if (sx >= srcW) sx = srcW - 1;
                int p = rowBase + sx * 4;
                int outIdx = outRow + dx;
                buffer[outIdx] = pixels[p + 2] * Scale;             // R plane
                buffer[outIdx + plane] = pixels[p + 1] * Scale;     // G plane
                buffer[outIdx + 2 * plane] = pixels[p] * Scale;     // B plane
            }
        }

        var tensor = new DenseTensor<float>(buffer, new[] { 1, 3, YoloInputSize, YoloInputSize });
        return (tensor, scaleX, scaleY);
    }

    // ------------------------------------------------------------------ Decoder

    /// <summary>
    /// Decodes the NMS-free YOLO26 output format. The detection tensor has
    /// shape (1, MaxDetections, K) where K is 6 for bbox-only or 38 for seg.
    /// Each row: [x1, y1, x2, y2, score, class_id, mask_coeff_0..31 if seg].
    /// Coordinates are in 640-input-pixel space; class_id is integer cast to
    /// float; score is sigmoid'd in [0, 1]. Empty slots have score 0.
    /// </summary>
    private IReadOnlyList<DetectedObject> DecodeNmsFree(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        float scoreThreshold,
        float scaleX,
        float scaleY,
        int sourceWidth,
        int sourceHeight)
    {
        var byName = outputs.ToDictionary(o => o.Name, o => o);
        if (!byName.TryGetValue(_detectionOutputName, out var detOut)) return Array.Empty<DetectedObject>();
        var det = detOut.AsTensor<float>();

        Tensor<float>? proto = null;
        if (_hasSegmentation && byName.TryGetValue(_protoOutputName, out var protoOut))
        {
            proto = protoOut.AsTensor<float>();
        }

        var dims = det.Dimensions;
        int numDetections = dims.Length >= 2 ? dims[1] : MaxDetections;
        int rowSize = dims.Length >= 3 ? dims[2] : (_hasSegmentation ? DetectionRowSize : 6);

        var result = new List<DetectedObject>();
        for (int i = 0; i < numDetections; i++)
        {
            float score = det[0, i, 4];
            if (score < scoreThreshold) continue;
            int cocoIdx = (int)Math.Round(det[0, i, 5]);
            if (!CocoMap.TryGetValue(cocoIdx, out var mapping)) continue;

            // Bbox in 640-pixel space, corner-form (x1, y1, x2, y2).
            float x1Model = det[0, i, 0];
            float y1Model = det[0, i, 1];
            float x2Model = det[0, i, 2];
            float y2Model = det[0, i, 3];

            // Scale to source pixels, clamp to frame.
            int x1 = Math.Max(0, (int)(x1Model * scaleX));
            int y1 = Math.Max(0, (int)(y1Model * scaleY));
            int x2 = Math.Min(sourceWidth, (int)(x2Model * scaleX));
            int y2 = Math.Min(sourceHeight, (int)(y2Model * scaleY));
            int w = x2 - x1;
            int h = y2 - y1;
            if (w <= 0 || h <= 0) continue;

            SegmentationMask? mask = null;
            if (proto is not null && rowSize >= DetectionRowSize)
            {
                mask = BuildSegmentationMask(
                    det, proto, i,
                    x1Model, y1Model, x2Model, y2Model,
                    sourceX: x1, sourceY: y1, sourceW: w, sourceH: h);
            }

            result.Add(new DetectedObject(
                Class: mapping.Category,
                RawClassLabel: mapping.Label,
                Confidence: score,
                Box: new BoundingBox(x1, y1, w, h),
                Mask: mask));
        }
        return result;
    }

    /// <summary>
    /// Combines the per-detection 32-coefficient vector with the (32, P, P)
    /// prototype tensor, sigmoids, thresholds at 0.5, crops to bbox in proto
    /// space, and resamples the cropped mask to the source-bbox dimensions.
    /// Returns a byte array of length sourceW * sourceH; 255 = object, 0 = bg.
    /// </summary>
    private static SegmentationMask? BuildSegmentationMask(
        Tensor<float> det, Tensor<float> proto, int detIdx,
        float bboxX1Model, float bboxY1Model, float bboxX2Model, float bboxY2Model,
        int sourceX, int sourceY, int sourceW, int sourceH)
    {
        if (sourceW <= 0 || sourceH <= 0) return null;

        // Crop the proto tensor to the bbox region in proto-space (160 / 640).
        const float ProtoScale = (float)ProtoSize / YoloInputSize;
        int px1 = Math.Max(0, (int)Math.Floor(bboxX1Model * ProtoScale));
        int py1 = Math.Max(0, (int)Math.Floor(bboxY1Model * ProtoScale));
        int px2 = Math.Min(ProtoSize, (int)Math.Ceiling(bboxX2Model * ProtoScale));
        int py2 = Math.Min(ProtoSize, (int)Math.Ceiling(bboxY2Model * ProtoScale));
        int pw = px2 - px1;
        int ph = py2 - py1;
        if (pw <= 0 || ph <= 0) return null;

        // Compute the cropped sigmoid mask: for each pixel (py, px) inside the
        // bbox-in-proto-space, mask[py, px] = sigmoid(sum_c coeff[c] * proto[c, py, px]).
        var protoMask = new float[ph * pw];
        for (int c = 0; c < NumProtoChannels; c++)
        {
            float coeff = det[0, detIdx, 6 + c];
            for (int yy = 0; yy < ph; yy++)
            {
                int srcY = py1 + yy;
                int outRow = yy * pw;
                for (int xx = 0; xx < pw; xx++)
                {
                    int srcX = px1 + xx;
                    protoMask[outRow + xx] += coeff * proto[0, c, srcY, srcX];
                }
            }
        }

        // Sigmoid and threshold at 0.5; resample to source-bbox dimensions with
        // nearest-neighbour. Output is a single-channel alpha byte array.
        var alpha = new byte[sourceW * sourceH];
        for (int yy = 0; yy < sourceH; yy++)
        {
            // Map source-y to proto-mask y. Proto-mask covers the bbox region,
            // so sourceY 0..sourceH maps to protoMask 0..ph.
            int py = Math.Min(ph - 1, (int)(yy * (float)ph / sourceH));
            int outRow = yy * sourceW;
            int srcRow = py * pw;
            for (int xx = 0; xx < sourceW; xx++)
            {
                int px = Math.Min(pw - 1, (int)(xx * (float)pw / sourceW));
                float v = Sigmoid(protoMask[srcRow + px]);
                alpha[outRow + xx] = v >= MaskBinaryThreshold ? (byte)255 : (byte)0;
            }
        }

        return new SegmentationMask(alpha, sourceW, sourceH);
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    // ------------------------------------------------------------------ Session

    private static (InferenceSession Session, string ProviderName) CreateSession(string modelPath)
    {
        try
        {
            var opts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };
            opts.AppendExecutionProvider_DML(0);
            return (new InferenceSession(modelPath, opts), "DirectML");
        }
        catch
        {
            var opts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };
            return (new InferenceSession(modelPath, opts), "CPU");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _wakeup.Writer.TryComplete();
        try
        {
            await _workerTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch { }
        try { _session.Dispose(); } catch { }
        _cts.Dispose();
    }
}
