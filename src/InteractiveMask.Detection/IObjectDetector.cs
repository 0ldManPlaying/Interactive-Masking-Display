using System;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveMask.Detection;

/// <summary>
/// Detector contract for the v2.0 AI-masking pipeline. Display owns at most one
/// <see cref="IObjectDetector"/> per session; concrete implementations are selected
/// based on host capability:
/// <list type="bullet">
///   <item><see cref="NullDetector"/> when AI is unavailable (Tier 0 host, model
///   missing, remote endpoint unreachable, or as a recovery target after a fault).</item>
///   <item><c>OnnxLocalDetector</c> for in-process inference via ONNX Runtime.</item>
///   <item><c>JetsonRemoteDetector</c> when an edge appliance is configured (v2.1).</item>
/// </list>
/// All implementations are expected to be thread-safe for the calling pattern
/// "InitializeAsync once, DetectAsync many times concurrently". DisposeAsync is
/// called exactly once at end-of-session.
/// </summary>
public interface IObjectDetector : IAsyncDisposable
{
    /// <summary>Backend descriptor. Read once after construction; does not change.</summary>
    DetectorCapability Capability { get; }

    /// <summary>Current operational status. Changes are surfaced via <see cref="StatusChanged"/>.</summary>
    DetectorStatus Status { get; }

    /// <summary>
    /// Raised when <see cref="Status"/> changes. Implementations must not raise this
    /// from inside a <see cref="DetectAsync"/> call without first marshalling to a
    /// fresh continuation; subscribers run on whatever thread the event fires from
    /// and Display will dispatch UI updates as needed.
    /// </summary>
    event EventHandler<DetectorStatus>? StatusChanged;

    /// <summary>
    /// One-time initialization: load models, open remote connections, allocate
    /// inference buffers. After successful return the detector should report
    /// <see cref="DetectorStatus.Ready"/>. Cancellation token is honoured.
    /// </summary>
    Task InitializeAsync(DetectorConfig config, CancellationToken ct = default);

    /// <summary>
    /// Runs detection on a single frame. Implementations may queue and coalesce
    /// frames internally; under load the returned <see cref="DetectionFrame"/> may
    /// correspond to an older timestamp if the detector dropped intermediate frames
    /// to stay within budget. Callers should rely on <see cref="DetectionFrame.FrameTimestampTicks"/>
    /// rather than the call order when correlating detections back to frames.
    /// </summary>
    ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default);
}
