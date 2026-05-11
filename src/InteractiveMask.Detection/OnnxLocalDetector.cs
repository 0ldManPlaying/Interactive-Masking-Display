using System;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveMask.Detection;

/// <summary>
/// In-process detector that runs ONNX models via Microsoft.ML.OnnxRuntime,
/// preferring the DirectML execution provider with CUDA / CPU as fallback.
/// <para>
/// <strong>P4 status:</strong> stub. <see cref="InitializeAsync"/> and
/// <see cref="DetectAsync"/> throw <see cref="NotImplementedException"/>. The full
/// implementation (model loading, pre/post-processing for YuNet + YOLOv8n,
/// per-class result mapping) lands in v2.0 main work; the abstraction is here
/// now so the surrounding wiring (Setup UI, detector lifecycle, audit events) can
/// be developed against a stable interface.
/// </para>
/// </summary>
public sealed class OnnxLocalDetector : IObjectDetector
{
    private static readonly DetectorCapability StaticCapability = new(
        BackendName: "OnnxLocalDetector",
        ModelDescription: "ONNX Runtime (DirectML / CUDA / CPU EP)",
        SupportedClasses: new[]
        {
            ObjectClass.Face,
            ObjectClass.Person,
            ObjectClass.TwoWheeler,
            ObjectClass.Vehicle,
            // LicensePlate is added in v2.0.x once the in-house model is shipped.
        },
        SupportsPolygonMasks: false);   // bbox-only in v2.0; polygon comes with v2.x seg upgrade.

    public DetectorCapability Capability => StaticCapability;

    public DetectorStatus Status { get; private set; } = DetectorStatus.Uninitialized;

#pragma warning disable CS0067 // Event never raised in stub; real implementation will raise it.
    public event EventHandler<DetectorStatus>? StatusChanged;
#pragma warning restore CS0067

    public Task InitializeAsync(DetectorConfig config, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "OnnxLocalDetector.InitializeAsync: implementation lands in v2.0 main work. " +
            "The P4 deliverable provides the abstraction and stubs; the actual model-loading " +
            "and pre/post-processing pipeline is the next major phase.");

    public ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "OnnxLocalDetector.DetectAsync: implementation lands in v2.0 main work.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
