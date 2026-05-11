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
/// M1 milestone (v2.0 prerequisite work): face-detection only via YuNet
/// (face_detection_yunet_2023mar.onnx). Multi-class output via YOLOv8n COCO is
/// added in M2, license-plate detection in v2.0.x.
/// </para>
/// <para>
/// Thread-safety: <see cref="InitializeAsync"/> must be called exactly once.
/// <see cref="DetectAsync"/> is safe to call concurrently after Initialize completes
/// because <see cref="InferenceSession"/> serialises Run() calls internally.
/// </para>
/// </summary>
public sealed class OnnxLocalDetector : IObjectDetector
{
    // YuNet 2023mar has a fixed 640x640 input. The model takes BGR float32 NCHW
    // without normalisation (values in 0..255).
    private const int YunetInputSize = 640;
    private const float DefaultFaceConfidence = 0.6f;
    private const float NmsIouThreshold = 0.4f;

    private static readonly int[] YunetStrides = { 8, 16, 32 };

    private InferenceSession? _faceSession;
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
        if (_faceSession != null)
        {
            throw new InvalidOperationException("OnnxLocalDetector.InitializeAsync called twice.");
        }

        SetStatus(DetectorStatus.Initializing);
        _config = config;

        try
        {
            await Task.Run(() =>
            {
                var modelPath = ResolveYunetModelPath();
                var (session, provider) = CreateSession(modelPath);
                _faceSession = session;
                _executionProvider = provider;
            }, ct).ConfigureAwait(false);

            _capability = new DetectorCapability(
                BackendName: "OnnxLocalDetector",
                ModelDescription: $"ONNX Runtime ({_executionProvider}) + YuNet faces",
                SupportedClasses: new[] { ObjectClass.Face },
                SupportsPolygonMasks: false);

            SetStatus(DetectorStatus.Ready);
        }
        catch
        {
            _faceSession?.Dispose();
            _faceSession = null;
            SetStatus(DetectorStatus.Unavailable);
            throw;
        }
    }

    public ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default)
    {
        if (_faceSession is null)
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

        // 1. Preprocess BGRA frame to 640x640 BGR float32 NCHW.
        var (inputTensor, scaleX, scaleY) = PreprocessForYunet(bitmapFrame);

        // 2. Run inference.
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
        };
        var detections = new List<Detection>();
        using (var outputs = _faceSession.Run(inputs))
        {
            // 3. Anchor-free decode + NMS, then scale back to source frame coordinates.
            var faceThreshold = ResolveFaceThreshold();
            detections.AddRange(DecodeYunet(outputs, faceThreshold, scaleX, scaleY, bitmapFrame.Width, bitmapFrame.Height));
        }

        // 4. Filter out classes the caller has disabled (defence in depth; YuNet only
        //    emits Face anyway, but keeps the contract honest for the multi-model M2).
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

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        _faceSession?.Dispose();
        _faceSession = null;
        SetStatus(DetectorStatus.Uninitialized);
    }

    // ------------------------------------------------------------------ Helpers

    private void SetStatus(DetectorStatus newStatus)
    {
        if (Status == newStatus) return;
        Status = newStatus;
        StatusChanged?.Invoke(this, newStatus);
    }

    private float ResolveFaceThreshold() =>
        _config?.ConfidenceThresholds.TryGetValue(ObjectClass.Face, out var t) == true
            ? t : DefaultFaceConfidence;

    private static string ResolveYunetModelPath()
    {
        const string fileName = "face_detection_yunet_2023mar.onnx";
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
            $"YuNet model not found. Expected at {candidates[0]}.");
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
    /// Resamples the source BGRA frame to a fixed 640x640 BGR float32 NCHW tensor.
    /// Returns the per-axis scale factors so the post-processor can map detections
    /// back to source coordinates. Resampling uses nearest-neighbour; YuNet is
    /// robust to that and we save the cost of bilinear interpolation.
    /// <para>
    /// The hot loop writes into a flat <c>float[]</c> instead of <c>tensor[c,y,x]</c>
    /// because DenseTensor's indexer recomputes the strided offset on every access,
    /// which dominates frame time. Direct buffer fills are about 20x faster.
    /// </para>
    /// </summary>
    private static (DenseTensor<float> Tensor, float ScaleX, float ScaleY) PreprocessForYunet(BitmapFrameRef frame)
    {
        float scaleX = (float)frame.Width / YunetInputSize;
        float scaleY = (float)frame.Height / YunetInputSize;

        // NCHW with C=3, H=W=640. Plane order is B, G, R (YuNet expects BGR).
        const int plane = YunetInputSize * YunetInputSize;
        var buffer = new float[3 * plane];

        var pixels = frame.BgraPixels;
        int stride = frame.Stride;
        int srcH = frame.Height;
        int srcW = frame.Width;

        // YuNet expects BGR float in 0..255 (no mean / std normalisation).
        for (int dy = 0; dy < YunetInputSize; dy++)
        {
            int sy = (int)(dy * scaleY);
            if (sy >= srcH) sy = srcH - 1;
            int rowBase = sy * stride;
            int outRow = dy * YunetInputSize;
            for (int dx = 0; dx < YunetInputSize; dx++)
            {
                int sx = (int)(dx * scaleX);
                if (sx >= srcW) sx = srcW - 1;
                int p = rowBase + sx * 4;       // BGRA8 source: B=0, G=1, R=2, A=3
                int outIdx = outRow + dx;
                buffer[outIdx] = pixels[p];                  // B plane
                buffer[outIdx + plane] = pixels[p + 1];      // G plane
                buffer[outIdx + 2 * plane] = pixels[p + 2];  // R plane
            }
        }

        var tensor = new DenseTensor<float>(buffer, new[] { 1, 3, YunetInputSize, YunetInputSize });
        return (tensor, scaleX, scaleY);
    }

    // ------------------------------------------------------------------ Postprocess

    private static IReadOnlyList<Detection> DecodeYunet(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        float scoreThreshold,
        float scaleX,
        float scaleY,
        int sourceWidth,
        int sourceHeight)
    {
        // Index the outputs by name for clarity. The 12 outputs are organised as
        // cls_{8,16,32}, obj_{8,16,32}, bbox_{8,16,32}, kps_{8,16,32}.
        var outputMap = outputs.ToDictionary(o => o.Name, o => o.AsTensor<float>());

        var candidates = new List<(float Score, float X1, float Y1, float X2, float Y2)>();

        foreach (var stride in YunetStrides)
        {
            var cls = outputMap[$"cls_{stride}"];
            var obj = outputMap[$"obj_{stride}"];
            var bbox = outputMap[$"bbox_{stride}"];

            int gridSize = YunetInputSize / stride;
            int anchorCount = gridSize * gridSize;

            for (int idx = 0; idx < anchorCount; idx++)
            {
                int gridX = idx % gridSize;
                int gridY = idx / gridSize;

                float clsLogit = cls[0, idx, 0];
                float objLogit = obj[0, idx, 0];
                // YuNet score is geometric mean of the two sigmoid probabilities.
                float score = MathF.Sqrt(Sigmoid(clsLogit) * Sigmoid(objLogit));
                if (score < scoreThreshold) continue;

                // Anchor-free decode: bbox offsets are relative to the grid cell's
                // origin, w/h are log-space in stride units.
                float dx = bbox[0, idx, 0];
                float dy = bbox[0, idx, 1];
                float dw = bbox[0, idx, 2];
                float dh = bbox[0, idx, 3];

                float cxModel = (gridX + dx) * stride;
                float cyModel = (gridY + dy) * stride;
                float wModel = MathF.Exp(dw) * stride;
                float hModel = MathF.Exp(dh) * stride;

                // Convert center-w-h to corners, still in model (640x640) coordinates.
                float x1Model = cxModel - wModel / 2f;
                float y1Model = cyModel - hModel / 2f;
                float x2Model = cxModel + wModel / 2f;
                float y2Model = cyModel + hModel / 2f;

                // Map back to source-frame pixel coordinates.
                float x1 = x1Model * scaleX;
                float y1 = y1Model * scaleY;
                float x2 = x2Model * scaleX;
                float y2 = y2Model * scaleY;

                candidates.Add((score, x1, y1, x2, y2));
            }
        }

        if (candidates.Count == 0) return Array.Empty<Detection>();

        // NMS: sort by score descending, greedily keep, suppress overlapping.
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<int>(candidates.Count);
        var suppressed = new bool[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            if (suppressed[i]) continue;
            kept.Add(i);
            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (suppressed[j]) continue;
                if (Iou(candidates[i], candidates[j]) > NmsIouThreshold)
                {
                    suppressed[j] = true;
                }
            }
        }

        // Build the final Detection list, clamping to frame bounds and dropping degenerate boxes.
        var result = new List<Detection>(kept.Count);
        foreach (var i in kept)
        {
            var c = candidates[i];
            int x = Math.Max(0, (int)c.X1);
            int y = Math.Max(0, (int)c.Y1);
            int w = Math.Min(sourceWidth, (int)c.X2) - x;
            int h = Math.Min(sourceHeight, (int)c.Y2) - y;
            if (w <= 0 || h <= 0) continue;

            result.Add(new Detection(
                Class: ObjectClass.Face,
                RawClassLabel: "face",
                Confidence: c.Score,
                Box: new BoundingBox(x, y, w, h),
                Mask: null));
        }
        return result;
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

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
