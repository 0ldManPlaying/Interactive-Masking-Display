using System;
using System.Collections.Generic;

namespace InteractiveMask.Hardware;

/// <summary>
/// Snapshot of the local host's hardware capabilities as observed at <see cref="ProbedAt"/>.
/// Drives the capability gate for v2.0 AI-masking features.
/// <para>
/// The profile is intentionally a flat, JSON-serialisable record so it can be persisted to
/// <c>%PROGRAMDATA%\InteractiveMask\capability-profile.json</c> and surfaced in Setup &#x2192;
/// About &#x2192; System capabilities without UI dependencies.
/// </para>
/// </summary>
public sealed record HostCapabilityProfile(
    DateTime ProbedAt,
    string OsVersion,
    string ProbeSchemaVersion,
    CpuInfo Cpu,
    MemoryInfo Memory,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<NpuInfo> Npus,
    IReadOnlyList<RemoteDetectorEndpoint> RemoteDetectors,
    BenchmarkResult? LastBenchmark,
    CapabilityTier Tier,
    IReadOnlyList<string> ProbeWarnings);

public sealed record CpuInfo(
    string Name,
    string Manufacturer,
    int LogicalCores,
    int PhysicalCores,
    int MaxClockMhz,
    bool HasAvx2);

public sealed record MemoryInfo(
    long TotalPhysicalBytes,
    long AvailablePhysicalBytes);

public sealed record GpuInfo(
    string Name,
    GpuVendor Vendor,
    long? DedicatedVramBytes,
    long? SharedMemoryBytes,
    string? DriverVersion,
    bool IsIntegratedHeuristic);

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel,
    Qualcomm,
    Microsoft
}

public sealed record NpuInfo(string Name, NpuVendor Vendor);

public enum NpuVendor
{
    Unknown,
    IntelAiBoost,
    AmdXdna,
    QualcommHexagon
}

public sealed record RemoteDetectorEndpoint(
    string Name,
    string Host,
    int Port,
    bool Reachable);

public sealed record BenchmarkResult(
    DateTime RunAt,
    double ColdStartMs,
    double SteadyStateP50Ms,
    double SteadyStateP95Ms,
    double SteadyStateP99Ms,
    double ThroughputFps,
    string ExecutionProvider,
    string ModelFileName,
    IReadOnlyList<int> InputDimensions);

/// <summary>
/// Capability tier classifier output. Higher tier means more AI features available.
/// Tier 0 (<see cref="Disabled"/>) means v2.0 AI features are unavailable; v1.x masking
/// still works on the same host.
/// </summary>
public enum CapabilityTier
{
    Disabled = 0,
    Light = 1,
    Standard = 2,
    Full = 3,
    Premium = 4
}
