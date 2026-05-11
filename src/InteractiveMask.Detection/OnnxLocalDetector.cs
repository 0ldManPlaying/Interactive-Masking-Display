using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace InteractiveMask.Detection;

/// <summary>
/// In-process detector that runs ONNX models via Microsoft.ML.OnnxRuntime with the
/// DirectML execution provider (CPU fallback if DirectML init fails).
/// <para>
/// M2 milestone: YOLOv8n COCO multi-class detection (Person / TwoWheeler / Vehicle).
/// LicensePlate via custom-trained model in v2.0.x. Face detection was on the
/// initial M3 v0.1 path via YuNet but dropped because faces on distant security
/// cameras are too small and produce too many false positives to be useful for
/// the retail use case.
/// </para>
/// <para>
/// Thread-safety: <see cref="InitializeAsync"/> must be called exactly once.
/// <see cref="DetectAsync"/> runs synchronously on the calling thread; concurrent
/// callers are serialised internally by <see cref="InferenceSession"/>'s mutex.
/// </para>
/// </summary>
public sealed class OnnxLocalDetector : IObjectDetector
{
    // YOLOv8n input: 640x640 RGB float32 NCHW, values normalised to [0, 1].
    private const int YoloInputSize = 640;
    private const float DefaultObjectConfidence = 0.4f;
    private const float NmsIouThreshold = 0.45f;

    // Output layout: (1, 84, 8400). 84 channels = 4 bbox coords + 80 COCO class
    // scores (already sigmoid'd by the Ultralytics export). 8400 anchors =
    // (80*80) + (40*40) + (20*20) across the three feature levels.
    private const int YoloNumClasses = 80;
    private const int YoloNumAnchors = 8400;
    private const int YoloOutputChannels = 4 + YoloNumClasses;

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

    private InferenceSession? _session;
    private DetectorConfig? _config;
    private string _executionProvider = "Uninitialized";

    private DetectorCapability _capability = new(
        BackendName: "OnnxLocalDetector",
        ModelDescription: "ONNX Runtime + DirectML (uninitialized)",
        SupportedClasses: Array.Empty<ObjectClass>(),
        SupportsPolygonMasks: false);

    public DetectorCapability Capability => _capability;

    public DetectorStatus Status { get; private set; } = DetectorStatus.Uninitialized;

    public event EventHandler<DetectorStatus>? StatusChanged;

    public async Task InitializeAsync(DetectorConfig config, CancellationToken ct = default)
    {
        if (_session != null)
        {
            throw new InvalidOperationException("OnnxLocalDetector.InitializeAsync called twice.");
        }

        SetStatus(DetectorStatus.Initializing);
        _config = config;

        try
        {
            await Task.Run(() =>
            {
                var modelPath = ResolveYoloModelPath();
                var (session, provider) = CreateSession(modelPath);
                _session = session;
                _executionProvider = provider;
            }, ct).ConfigureAwait(false);

            _capability = new DetectorCapability(
                BackendName: "OnnxLocalDetector",
                ModelDescription: $"ONNX Runtime ({_executionProvider}) + YOLOv8n COCO (Person / TwoWheeler / Vehicle)",
                SupportedClasses: new[] { ObjectClass.Person, ObjectClass.TwoWheeler, ObjectClass.Vehicle },
                SupportsPolygonMasks: false);

            SetStatus(DetectorStatus.Ready);
        }
        catch
        {
            _session?.Dispose();
            _session = null;
            SetStatus(DetectorStatus.Unavailable);
            throw;
        }
    }

    public ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("OnnxLocalDetector not initialized.");
        }
        if (frame is not BitmapFrameRef bitmapFrame)
        {
            throw new ArgumentException(
                $"OnnxLocalDetector requires a {nameof(BitmapFrameRef)}; got {frame.GetType().Name}.",
                nameof(frame));
        }
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();

        // 1. Preprocess BGRA frame to 640x640 RGB float32 NCHW, normalised to [0, 1].
        var (inputTensor, scaleX, scaleY) = PreprocessForYolo(bitmapFrame);

        // 2. Run inference.
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", inputTensor),
        };
        var detections = new List<DetectedObject>();
        using (var outputs = _session.Run(inputs))
        {
            // 3. Decode YOLOv8 output, filter by class + confidence, per-class NMS,
            //    scale bbox back to source-frame pixel coordinates.
            var threshold = ResolveObjectConfidence();
            detections.AddRange(DecodeYolo(outputs, threshold, scaleX, scaleY, bitmapFrame.Width, bitmapFrame.Height));
        }

        // 4. Filter out classes the caller has disabled (defence in depth; the
        //    COCO mapping already only emits Person / TwoWheeler / Vehicle).
        if (_config?.EnabledClasses is { Count: > 0 } enabled)
        {
            detections = detections.Where(d => enabled.Contains(d.Class)).ToList();
        }

        sw.Stop();
        return ValueTask.FromResult(new DetectionFrame(
            FrameTimestampTicks: bitmapFrame.TimestampTicks,
            StreamId: bitmapFrame.StreamId,
            Detections: detections,
            Metrics: new DetectorMetrics(
                InferenceLatencyMs: sw.Elapsed.TotalMilliseconds,
                QueueDepth: 0,
                GpuUtilizationPercent: null)));
    }

    public ValueTask DisposeAsync()
    {
        // Synchronous on purpose; see notes on the original M1 implementation.
        _session?.Dispose();
        _session = null;
        Status = DetectorStatus.Uninitialized;
        return ValueTask.CompletedTask;
    }

    // ------------------------------------------------------------------ Helpers

    private void SetStatus(DetectorStatus newStatus)
    {
        if (Status == newStatus) return;
        Status = newStatus;
        StatusChanged?.Invoke(this, newStatus);
    }

    private float ResolveObjectConfidence()
    {
        // The caller's threshold dict is per-category; we use the strictest of the
        // Person / TwoWheeler / Vehicle thresholds as the cut-off applied during
        // decode (further per-category filtering happens at the caller via
        // EnabledClasses or post-hoc threshold checks). Falls back to a sensible
        // default if none configured.
        if (_config is null) return DefaultObjectConfidence;
        float min = float.MaxValue;
        foreach (var c in new[] { ObjectClass.Person, ObjectClass.TwoWheeler, ObjectClass.Vehicle })
        {
            if (_config.ConfidenceThresholds.TryGetValue(c, out var t) && t < min) min = t;
        }
        return min == float.MaxValue ? DefaultObjectConfidence : min;
    }

    private static string ResolveYoloModelPath()
    {
        const string fileName = "yolov8n.onnx";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "models", fileName),
            Path.Combine(Path.GetDirectoryName(typeof(OnnxLocalDetector).Assembly.Location) ?? string.Empty, "models", fileName),
        };
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
        }
        throw new FileNotFoundException(
            $"YOLOv8n model not found. Expected at {candidates[0]}.");
    }

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

    // ------------------------------------------------------------------ Preprocess

    /// <summary>
    /// Resamples the source BGRA frame to a fixed 640x640 RGB float32 NCHW tensor,
    /// normalised to [0, 1]. Returns the per-axis scale factors so the post-processor
    /// can map detections back to source-frame coordinates.
    /// <para>
    /// The hot loop writes into a flat <c>float[]</c> rather than via
    /// <c>tensor[c,y,x]</c> indexer because DenseTensor's indexer recomputes the
    /// strided offset on every access (about 20x slower than direct buffer fills).
    /// </para>
    /// </summary>
    private static (DenseTensor<float> Tensor, float ScaleX, float ScaleY) PreprocessForYolo(BitmapFrameRef frame)
    {
        float scaleX = (float)frame.Width / YoloInputSize;
        float scaleY = (float)frame.Height / YoloInputSize;

        // NCHW with C=3, H=W=640. Plane order is R, G, B (YOLOv8 expects RGB).
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
                int p = rowBase + sx * 4;          // BGRA source: B=0, G=1, R=2, A=3
                int outIdx = outRow + dx;
                buffer[outIdx] = pixels[p + 2] * Scale;             // R plane
                buffer[outIdx + plane] = pixels[p + 1] * Scale;     // G plane
                buffer[outIdx + 2 * plane] = pixels[p] * Scale;     // B plane
            }
        }

        var tensor = new DenseTensor<float>(buffer, new[] { 1, 3, YoloInputSize, YoloInputSize });
        return (tensor, scaleX, scaleY);
    }

    // ------------------------------------------------------------------ Postprocess

    private static IReadOnlyList<DetectedObject> DecodeYolo(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        float scoreThreshold,
        float scaleX,
        float scaleY,
        int sourceWidth,
        int sourceHeight)
    {
        var tensor = outputs.First().AsTensor<float>();

        // Per-class candidate lists for per-class NMS.
        var perClass = new Dictionary<int, List<(float Score, float X1, float Y1, float X2, float Y2)>>();

        for (int i = 0; i < YoloNumAnchors; i++)
        {
            // bbox channels 0..3 (cx, cy, w, h) in input-pixel space (0..640).
            float cx = tensor[0, 0, i];
            float cy = tensor[0, 1, i];
            float w = tensor[0, 2, i];
            float h = tensor[0, 3, i];

            // Find best class among the indices we care about. Iterating all 80
            // classes just to argmax is wasteful when most are not maskable;
            // we walk the CocoMap keys and pick the highest one.
            float bestScore = 0;
            int bestCocoIdx = -1;
            foreach (var cocoIdx in CocoMap.Keys)
            {
                float score = tensor[0, 4 + cocoIdx, i];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCocoIdx = cocoIdx;
                }
            }
            if (bestCocoIdx < 0 || bestScore < scoreThreshold) continue;

            // Convert center-wh to corners (still in 640-pixel input space), then
            // scale to source-frame pixel coordinates.
            float x1Model = cx - w / 2f;
            float y1Model = cy - h / 2f;
            float x2Model = cx + w / 2f;
            float y2Model = cy + h / 2f;
            float x1 = x1Model * scaleX;
            float y1 = y1Model * scaleY;
            float x2 = x2Model * scaleX;
            float y2 = y2Model * scaleY;

            if (!perClass.TryGetValue(bestCocoIdx, out var list))
            {
                list = new List<(float, float, float, float, float)>();
                perClass[bestCocoIdx] = list;
            }
            list.Add((bestScore, x1, y1, x2, y2));
        }

        if (perClass.Count == 0) return Array.Empty<DetectedObject>();

        // Per-class NMS, then merge into a single DetectedObject list.
        var result = new List<DetectedObject>();
        foreach (var (cocoIdx, candidates) in perClass)
        {
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            var suppressed = new bool[candidates.Count];
            var (category, label) = CocoMap[cocoIdx];
            for (int i = 0; i < candidates.Count; i++)
            {
                if (suppressed[i]) continue;
                var c = candidates[i];
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (suppressed[j]) continue;
                    if (Iou(c, candidates[j]) > NmsIouThreshold) suppressed[j] = true;
                }

                int xClamped = Math.Max(0, (int)c.X1);
                int yClamped = Math.Max(0, (int)c.Y1);
                int wClamped = Math.Min(sourceWidth, (int)c.X2) - xClamped;
                int hClamped = Math.Min(sourceHeight, (int)c.Y2) - yClamped;
                if (wClamped <= 0 || hClamped <= 0) continue;

                result.Add(new DetectedObject(
                    Class: category,
                    RawClassLabel: label,
                    Confidence: c.Score,
                    Box: new BoundingBox(xClamped, yClamped, wClamped, hClamped),
                    Mask: null));
            }
        }
        return result;
    }

    private static float Iou(
        (float Score, float X1, float Y1, float X2, float Y2) a,
        (float Score, float X1, float Y1, float X2, float Y2) b)
    {
        float interX1 = MathF.Max(a.X1, b.X1);
        float interY1 = MathF.Max(a.Y1, b.Y1);
        float interX2 = MathF.Min(a.X2, b.X2);
        float interY2 = MathF.Min(a.Y2, b.Y2);
        float interW = MathF.Max(0, interX2 - interX1);
        float interH = MathF.Max(0, interY2 - interY1);
        float interArea = interW * interH;
        if (interArea <= 0) return 0;
        float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
        float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
        return interArea / (areaA + areaB - interArea);
    }
}
