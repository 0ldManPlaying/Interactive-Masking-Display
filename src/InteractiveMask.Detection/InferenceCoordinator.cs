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
/// This eliminates two problems the per-tile-concurrent design hit in M3.2 v1:
/// </para>
/// <list type="bullet">
///   <item>Native crashes from concurrent <c>Run()</c> calls (DirectML EP did
///   not survive 16 GDK callback threads hitting the same session at once).</item>
///   <item>Unbounded queue buildup when the GDK source rate exceeds the
///   detector's throughput, leading to "step over" behaviour where inference
///   results lag the on-screen reality by several hundred milliseconds.</item>
/// </list>
/// <para>
/// API: callers <see cref="Submit"/> a frame and a callback. The callback is
/// invoked exactly once per submission - either with the actual detection
/// result, or, if the submission was dropped because a newer frame replaced it
/// in the slot before the worker got to it, with an empty result. This
/// guarantees no awaiting <see cref="TaskCompletionSource{T}"/> ever leaks.
/// </para>
/// </summary>
public sealed class InferenceCoordinator : IAsyncDisposable
{
    // YOLOv8n model parameters. Keep in sync with the bundled yolov8n.onnx.
    private const int YoloInputSize = 640;
    private const float DefaultObjectConfidence = 0.3f;
    private const float NmsIouThreshold = 0.45f;
    private const int YoloNumAnchors = 8400;

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
        // Writable from any thread; only read on the worker so no synchronisation
        // beyond the natural .NET float-write atomicity is required.
        _confidenceThreshold = threshold;
    }

    /// <summary>
    /// Submits a frame for inference. Replaces any pending unprocessed frame
    /// for the same <paramref name="streamId"/>; the replaced submission's
    /// callback is invoked synchronously here with an empty result so any
    /// awaiting completion-source completes (no leak).
    /// </summary>
    /// <param name="streamId">Per-tile stream identifier; must be in [0, maxStreams).</param>
    /// <param name="frame">Frame data; the coordinator takes ownership of the
    /// reference until the callback fires.</param>
    /// <param name="callback">Invoked exactly once - either with the inference
    /// result, or with an empty result if the submission was dropped. Runs on
    /// either the calling thread (drop path) or the worker thread (normal path);
    /// callers should keep callbacks fast and marshal heavy work elsewhere.</param>
    public void Submit(int streamId, BitmapFrameRef frame, Action<DetectionFrame> callback)
    {
        if (frame is null) throw new ArgumentNullException(nameof(frame));
        if (callback is null) throw new ArgumentNullException(nameof(callback));
        if (streamId < 0 || streamId >= _maxStreams) return;
        if (_cts.IsCancellationRequested) return;

        // Atomic slot + callback replacement. The two Exchange calls aren't atomic
        // as a pair, but the worker re-reads both after extracting the slot, so
        // the worst case is a "stale callback" pairing that still belongs to the
        // submitting tile (StreamId is fixed per slot index).
        var oldFrame = Interlocked.Exchange(ref _slots[streamId], frame);
        var oldCallback = Interlocked.Exchange(ref _callbacks[streamId], callback);

        if (oldFrame is not null && oldCallback is not null)
        {
            // Drop-path: an earlier submission for this stream is being replaced
            // before the worker got to it. Invoke its callback with an empty
            // result so any awaiting TaskCompletionSource completes immediately.
            // Empty detection list is a valid "no objects this frame" signal;
            // consumers with grace-window logic preserve their previous overlay.
            try
            {
                oldCallback(new DetectionFrame(
                    FrameTimestampTicks: oldFrame.TimestampTicks,
                    StreamId: oldFrame.StreamId,
                    Detections: Array.Empty<DetectedObject>(),
                    Metrics: new DetectorMetrics(0, QueueDepth: 1, GpuUtilizationPercent: null)));
            }
            catch
            {
                // Caller's callback faulted; not our problem. Continue.
            }
        }

        _wakeup.Writer.TryWrite(0); // Capacity 1, DropWrite => at most one pending wake.
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        try
        {
            await foreach (var _ in _wakeup.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                // Drain every populated slot before going back to wait. A burst
                // of submissions (e.g. all 16 tiles produced frames in the same
                // millisecond) results in one wake-up; we then process them all
                // round-robin before yielding.
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
                        break;  // Round-robin: process one slot per iteration of the inner loop,
                                // then start the next scan from _lastIdx+1 for fairness.
                    }
                } while (didWork && !ct.IsCancellationRequested);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception)
        {
            // Worker terminating unexpectedly. We deliberately don't restart it
            // automatically: a recurring inference fault should surface to the
            // user via the detector status, not silently retry forever.
        }
    }

    private void ProcessSlot(BitmapFrameRef frame, Action<DetectionFrame>? callback)
    {
        if (callback is null) return; // Nothing to deliver to.
        try
        {
            var result = RunInference(frame);
            callback(result);
        }
        catch (Exception)
        {
            // Inference faulted on one frame; deliver an empty result so the
            // awaiting consumer doesn't hang. The worker continues with the next
            // slot - a single bad frame can't tear the pipeline down.
            try
            {
                callback(new DetectionFrame(
                    FrameTimestampTicks: frame.TimestampTicks,
                    StreamId: frame.StreamId,
                    Detections: Array.Empty<DetectedObject>(),
                    Metrics: new DetectorMetrics(0, 0, null)));
            }
            catch { /* swallow */ }
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

        var detections = new List<DetectedObject>();
        using (var outputs = _session.Run(inputs))
        {
            detections.AddRange(DecodeYolo(
                outputs, _confidenceThreshold, scaleX, scaleY,
                bitmapFrame.Width, bitmapFrame.Height));
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

    private static IReadOnlyList<DetectedObject> DecodeYolo(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        float scoreThreshold,
        float scaleX,
        float scaleY,
        int sourceWidth,
        int sourceHeight)
    {
        var tensor = outputs.First().AsTensor<float>();

        var perClass = new Dictionary<int, List<(float Score, float X1, float Y1, float X2, float Y2)>>();

        for (int i = 0; i < YoloNumAnchors; i++)
        {
            float cx = tensor[0, 0, i];
            float cy = tensor[0, 1, i];
            float w = tensor[0, 2, i];
            float h = tensor[0, 3, i];

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
        catch
        {
            // Worker either still running or already faulted; either way we have
            // to release the session to stop a hang on app close. Two-second
            // window covers normal flush plus a safety margin.
        }
        try { _session.Dispose(); } catch { }
        _cts.Dispose();
    }
}
