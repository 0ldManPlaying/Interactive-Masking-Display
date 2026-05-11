using System.Collections.Generic;
using System.Linq;

namespace InteractiveMask.Hardware;

/// <summary>
/// Maps a <see cref="HostCapabilityProfile"/> to a <see cref="CapabilityTier"/>.
/// Thresholds are indicative; they will be re-calibrated against P3 benchmark output
/// once that component is in place. The benchmark result is the final arbiter, this
/// evaluator is the up-front static estimate before any inference work has run.
/// </summary>
public static class CapabilityTierEvaluator
{
    /// <summary>Hard floor: without AVX2 ONNX Runtime is impractically slow.</summary>
    public const bool RequiresAvx2 = true;

    /// <summary>Hard floor: ONNX models + WPF + GDK live below 8 GB is unrealistic.</summary>
    public const double MinimumRamGb = 8.0;

    public static CapabilityTier Evaluate(
        CpuInfo cpu,
        MemoryInfo memory,
        IReadOnlyList<GpuInfo> gpus,
        IReadOnlyList<NpuInfo> npus,
        IReadOnlyList<RemoteDetectorEndpoint> remotes)
    {
        if (RequiresAvx2 && !cpu.HasAvx2) return CapabilityTier.Disabled;

        var ramGb = memory.TotalPhysicalBytes / (1024.0 * 1024 * 1024);
        if (ramGb < MinimumRamGb) return CapabilityTier.Disabled;

        // A reachable remote detector (Jetson, future Hailo box, ...) is treated as
        // tier Full regardless of local hardware. The remote is doing the heavy lifting.
        if (remotes.Any(r => r.Reachable)) return CapabilityTier.Full;

        var bestDgpuVramGb = gpus
            .Where(g => !g.IsIntegratedHeuristic && g.DedicatedVramBytes.HasValue)
            .Select(g => g.DedicatedVramBytes!.Value / (1024.0 * 1024 * 1024))
            .DefaultIfEmpty(0.0)
            .Max();

        var hasIntegratedGpu = gpus.Any(g => g.IsIntegratedHeuristic);
        var hasKnownNpu = npus.Any(n => n.Vendor != NpuVendor.Unknown);

        if (bestDgpuVramGb >= 12.0) return CapabilityTier.Premium;
        if (bestDgpuVramGb >= 8.0) return CapabilityTier.Full;
        if (bestDgpuVramGb >= 4.0) return CapabilityTier.Standard;
        if (bestDgpuVramGb >= 1.0) return CapabilityTier.Light;
        if (hasIntegratedGpu || hasKnownNpu) return CapabilityTier.Light;

        return CapabilityTier.Disabled;
    }
}
