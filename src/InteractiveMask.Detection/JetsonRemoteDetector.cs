using System;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveMask.Detection;

/// <summary>
/// Out-of-process detector that talks to a Jetson Orin Nano sidecar over gRPC
/// (mutual TLS). The Jetson decodes the IDIS stream itself via the IDIS GDK
/// ARM64 binding and sends only detection metadata back; frames never travel
/// over the wire.
/// <para>
/// <strong>P4 status:</strong> stub. The full implementation lands in v2.1 (ARM port)
/// per the sequencing decision in <c>docs/architecture-v2-ai.md</c>. The stub is
/// included in v2.0 so Display-side code paths can target the same interface
/// today and the v2.1 port is purely an additive implementation, not a refactor.
/// </para>
/// </summary>
public sealed class JetsonRemoteDetector : IObjectDetector
{
    private static readonly DetectorCapability StaticCapability = new(
        BackendName: "JetsonRemoteDetector",
        ModelDescription: "Remote Jetson Orin sidecar (TensorRT via gRPC / mTLS)",
        SupportedClasses: new[]
        {
            ObjectClass.Face,
            ObjectClass.Person,
            ObjectClass.TwoWheeler,
            ObjectClass.Vehicle,
        },
        SupportsPolygonMasks: false);

    public DetectorCapability Capability => StaticCapability;

    public DetectorStatus Status { get; private set; } = DetectorStatus.Uninitialized;

#pragma warning disable CS0067 // Event never raised in stub; real implementation will raise it.
    public event EventHandler<DetectorStatus>? StatusChanged;
#pragma warning restore CS0067

    public Task InitializeAsync(DetectorConfig config, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "JetsonRemoteDetector.InitializeAsync: implementation lands in v2.1 (Jetson ARM port). " +
            "The gRPC client, mTLS pairing flow and Jetson-side service all ship in v2.1.");

    public ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "JetsonRemoteDetector.DetectAsync: implementation lands in v2.1 (Jetson ARM port).");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
