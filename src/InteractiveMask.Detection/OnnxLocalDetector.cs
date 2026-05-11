using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveMask.Detection;

/// <summary>
/// <see cref="IObjectDetector"/> facade over an <see cref="InferenceCoordinator"/>.
/// Existed as a fully self-contained inference owner in M3 v0.1; refactored in
/// M3.2 v2 so all <c>InferenceSession.Run()</c> calls happen from a single
/// worker thread inside the coordinator. The detector itself is now just a
/// thin contract adapter:
/// <list type="bullet">
///   <item><see cref="InitializeAsync"/> creates the coordinator.</item>
///   <item><see cref="DetectAsync"/> submits the frame to the coordinator with a
///   callback-driven <see cref="TaskCompletionSource{TResult}"/>, then awaits
///   that TCS.</item>
///   <item><see cref="DisposeAsync"/> tears the coordinator down with a bounded
///   timeout so app shutdown can't hang on an in-flight inference.</item>
/// </list>
/// <para>
/// Concurrent <see cref="DetectAsync"/> calls from different tiles are safe:
/// each one ends up as a Submit into a different slot index inside the
/// coordinator, and the coordinator's single worker thread serialises the
/// underlying ORT calls without any thread-on-thread contention.
/// </para>
/// </summary>
public sealed class OnnxLocalDetector : IObjectDetector
{
    private const float DefaultObjectConfidence = 0.3f;

    private InferenceCoordinator? _coordinator;
    private DetectorConfig? _config;

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
        if (_coordinator != null)
        {
            throw new InvalidOperationException("OnnxLocalDetector.InitializeAsync called twice.");
        }

        SetStatus(DetectorStatus.Initializing);
        _config = config;

        InferenceCoordinator? built = null;
        try
        {
            string resolvedModelName = "?";
            await Task.Run(() =>
            {
                var modelPath = ResolveYoloModelPath();
                resolvedModelName = Path.GetFileNameWithoutExtension(modelPath);
                built = new InferenceCoordinator(modelPath);
                built.SetConfidenceThreshold(ResolveObjectConfidence());
            }, ct).ConfigureAwait(false);

            _coordinator = built;

            _capability = new DetectorCapability(
                BackendName: "OnnxLocalDetector",
                ModelDescription: $"ONNX Runtime ({_coordinator!.ExecutionProvider}) + {resolvedModelName.ToUpperInvariant()} COCO (centralized worker)",
                SupportedClasses: new[] { ObjectClass.Person, ObjectClass.TwoWheeler, ObjectClass.Vehicle },
                SupportsPolygonMasks: false);

            SetStatus(DetectorStatus.Ready);
        }
        catch
        {
            if (built is not null)
            {
                try { await built.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            _coordinator = null;
            SetStatus(DetectorStatus.Unavailable);
            throw;
        }
    }

    public ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default)
    {
        var coordinator = _coordinator;
        if (coordinator is null)
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

        // RunContinuationsAsynchronously so the awaiter's continuation can't run
        // inline inside the coordinator's worker callback - that would block the
        // worker until the awaiter's downstream work (BeginInvoke + render path)
        // finished, defeating the point of the single-worker design.
        var tcs = new TaskCompletionSource<DetectionFrame>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Cancellation hookup: if the caller cancels, cancel the awaiter so the
        // surrounding caller doesn't hang. The coordinator callback still fires
        // when the slot is processed or replaced, but TrySetResult is a no-op
        // after TrySetCanceled, so there's no consistency hazard.
        CancellationTokenRegistration registration = default;
        if (ct.CanBeCanceled)
        {
            registration = ct.Register(static state =>
            {
                var t = (TaskCompletionSource<DetectionFrame>)state!;
                t.TrySetCanceled();
            }, tcs);
        }

        coordinator.Submit(bitmapFrame.StreamId, bitmapFrame, result =>
        {
            tcs.TrySetResult(result);
            registration.Dispose();
        });

        return new ValueTask<DetectionFrame>(tcs.Task);
    }

    public async ValueTask DisposeAsync()
    {
        var coordinator = _coordinator;
        _coordinator = null;
        if (coordinator is not null)
        {
            try
            {
                await coordinator.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Shutdown path: never let dispose throw.
            }
        }
        Status = DetectorStatus.Uninitialized;
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
        // M3.5 baseline: YOLO26n-seg (NMS-free, segmentation, ~4-5 ms warm pass
        // on RTX 3090 via DirectML EP). yolo26n.onnx (detection-only) is the
        // fallback if no seg variant is present. Older anchor-based models
        // (yolo11n / yolov8n) are no longer supported by the decoder; if a
        // legacy file is in the folder it will be ignored.
        // Order of preference: largest available seg variant first so admins
        // get the best small-object recall without code changes if they later
        // drop in a beefier model. Falls back through s -> n -> n-detect-only.
        var fileNames = new[] { "yolo26s-seg.onnx", "yolo26n-seg.onnx", "yolo26n.onnx" };
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "models"),
            Path.Combine(Path.GetDirectoryName(typeof(OnnxLocalDetector).Assembly.Location) ?? string.Empty, "models"),
        };
        foreach (var name in fileNames)
        {
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                var candidate = Path.Combine(root, name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        throw new FileNotFoundException(
            $"YOLO26 model not found. Expected yolo26n-seg.onnx or yolo26n.onnx under {roots[0]}.");
    }
}
