using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveMask.Detection;

/// <summary>
/// Fail-safe detector. Used when AI masking is unavailable for any reason:
/// Tier 0 host, benchmark-disabled, model file missing, remote endpoint unreachable,
/// or as the recovery target when a real backend faults and the render pipeline
/// rolls back to safe-state masking.
/// <para>
/// <see cref="DetectAsync"/> always returns an empty detection list. The render
/// layer interprets the absence of detections from a <see cref="NullDetector"/>
/// as the trigger to apply full-tile blur on bound tiles, matching the v1.x
/// privacy-first default. A privacy leak via missed AI detection is therefore
/// impossible: the worst-case outcome is "everything is blurred".
/// </para>
/// </summary>
public sealed class NullDetector : IObjectDetector
{
    private static readonly DetectorCapability StaticCapability = new(
        BackendName: "NullDetector",
        ModelDescription: "No model loaded (privacy-first fail-safe stand-in)",
        SupportedClasses: Array.Empty<ObjectClass>(),
        SupportsPolygonMasks: false);

    public DetectorCapability Capability => StaticCapability;

    public DetectorStatus Status { get; private set; } = DetectorStatus.Unavailable;

    public event EventHandler<DetectorStatus>? StatusChanged;

    public Task InitializeAsync(DetectorConfig config, CancellationToken ct = default)
    {
        // No initialization work. Status stays Unavailable so the render layer
        // routes every tile to its v1.x full-tile blur fallback. Subscribers that
        // attached AFTER construction still get notified once, so they can wire
        // their UI state from a single event source rather than two.
        var oldStatus = Status;
        Status = DetectorStatus.Unavailable;
        if (oldStatus != Status)
        {
            StatusChanged?.Invoke(this, Status);
        }
        return Task.CompletedTask;
    }

    public ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new DetectionFrame(
            FrameTimestampTicks: frame.TimestampTicks,
            StreamId: frame.StreamId,
            Detections: Array.Empty<DetectedObject>(),
            Metrics: new DetectorMetrics(
                InferenceLatencyMs: 0,
                QueueDepth: 0,
                GpuUtilizationPercent: null)));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
