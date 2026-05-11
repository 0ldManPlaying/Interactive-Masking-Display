using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace InteractiveMask.Hardware;

/// <summary>
/// Probes the local host for CPU, memory, GPU and NPU information used by the v2.0
/// capability gate. All probe steps are defensive: failures yield warnings and
/// best-effort partial data, never exceptions to the caller.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HostCapabilityProbe
{
    /// <summary>Schema version of the probe output. Bumped whenever fields change.</summary>
    public const string SchemaVersion = "1";

    /// <summary>
    /// Runs all probes and returns a fully-populated <see cref="HostCapabilityProfile"/>.
    /// Call from a background thread; WMI queries can take several hundred milliseconds
    /// on first access.
    /// </summary>
    public static HostCapabilityProfile Probe()
    {
        var warnings = new List<string>();

        var cpu = SafeProbe(ProbeCpu, warnings, "cpu",
            () => new CpuInfo("Unknown", "Unknown", Environment.ProcessorCount, 0, 0, false));

        var memory = SafeProbe(ProbeMemory, warnings, "memory",
            () => new MemoryInfo(0, 0));

        var gpus = SafeProbe(() => ProbeGpus(memory), warnings, "gpu",
            () => (IReadOnlyList<GpuInfo>)Array.Empty<GpuInfo>());

        var npus = SafeProbe(ProbeNpus, warnings, "npu",
            () => (IReadOnlyList<NpuInfo>)Array.Empty<NpuInfo>());

        // Remote detector probing is stubbed out in v0.1 of the module.
        // Once the Setup UI allows configuring Jetson endpoints (v2.1 prep), this
        // method will reach those endpoints over gRPC and populate Reachable.
        var remotes = (IReadOnlyList<RemoteDetectorEndpoint>)Array.Empty<RemoteDetectorEndpoint>();

        BenchmarkResult? lastBench = null; // P3 deliverable will populate this.

        var tier = CapabilityTierEvaluator.Evaluate(cpu, memory, gpus, npus, remotes);

        return new HostCapabilityProfile(
            ProbedAt: DateTime.UtcNow,
            OsVersion: Environment.OSVersion.Version.ToString(),
            ProbeSchemaVersion: SchemaVersion,
            Cpu: cpu,
            Memory: memory,
            Gpus: gpus,
            Npus: npus,
            RemoteDetectors: remotes,
            LastBenchmark: lastBench,
            Tier: tier,
            ProbeWarnings: warnings);
    }

    private static T SafeProbe<T>(Func<T> probe, List<string> warnings, string area, Func<T> fallback)
    {
        try
        {
            return probe();
        }
        catch (Exception ex)
        {
            warnings.Add($"{area}-probe-failed: {ex.GetType().Name}: {ex.Message}");
            return fallback();
        }
    }

    // ------------------------------------------------------------------ CPU

    private static CpuInfo ProbeCpu()
    {
        string name = "Unknown";
        string manufacturer = "Unknown";
        int logical = Environment.ProcessorCount;
        int physical = 0;
        int maxClock = 0;

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, Manufacturer, NumberOfLogicalProcessors, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            name = obj["Name"]?.ToString()?.Trim() ?? name;
            manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? manufacturer;
            if (obj["NumberOfLogicalProcessors"] is uint logCnt) logical = (int)logCnt;
            if (obj["NumberOfCores"] is uint coreCnt) physical = (int)coreCnt;
            if (obj["MaxClockSpeed"] is uint mhz) maxClock = (int)mhz;
            break; // Single-package assumption; multi-socket workstations are out of scope.
        }

        bool hasAvx2 = NativeMethods.IsProcessorFeaturePresent(NativeMethods.PF_AVX2_INSTRUCTIONS_AVAILABLE);
        return new CpuInfo(name, manufacturer, logical, physical, maxClock, hasAvx2);
    }

    // --------------------------------------------------------------- Memory

    private static MemoryInfo ProbeMemory()
    {
        var ms = new NativeMethods.MEMORYSTATUSEX();
        if (!NativeMethods.GlobalMemoryStatusEx(ms))
        {
            return new MemoryInfo(0, 0);
        }
        return new MemoryInfo(
            TotalPhysicalBytes: (long)ms.ullTotalPhys,
            AvailablePhysicalBytes: (long)ms.ullAvailPhys);
    }

    // ------------------------------------------------------------------ GPU

    private static IReadOnlyList<GpuInfo> ProbeGpus(MemoryInfo systemMemory)
    {
        // 1. Enumerate via WMI for names, vendor hint, driver, fallback VRAM.
        var raw = new List<(string Name, GpuVendor Vendor, string? DriverVersion, long? WmiVram)>();

        using (var searcher = new ManagementObjectSearcher(
            "SELECT Name, AdapterCompatibility, AdapterRAM, DriverVersion FROM Win32_VideoController"))
        {
            foreach (var obj in searcher.Get())
            {
                var n = obj["Name"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(n)) continue;
                var vendor = ClassifyVendor(obj["AdapterCompatibility"]?.ToString(), n);
                var driver = obj["DriverVersion"]?.ToString();
                // AdapterRAM is uint32 in WMI; caps at ~4 GB. Used only as a fallback when
                // the registry probe yields nothing.
                long? wmiVram = null;
                if (obj["AdapterRAM"] is uint ramBytes && ramBytes > 0)
                {
                    wmiVram = ramBytes;
                }
                raw.Add((n, vendor, driver, wmiVram));
            }
        }

        // 2. Pull accurate VRAM from registry (Windows 10+ exposes qwMemorySize REG_QWORD).
        var registryVram = ReadRegistryVram();

        // 3. Merge and classify.
        var result = new List<GpuInfo>(raw.Count);
        foreach (var (name, vendor, driver, wmiVram) in raw)
        {
            long? vram = MatchRegistryVram(name, registryVram) ?? wmiVram;
            bool isIntegrated = ClassifyIntegrated(name, vendor, vram);
            long? shared = isIntegrated && systemMemory.TotalPhysicalBytes > 0
                ? systemMemory.TotalPhysicalBytes / 2
                : null;
            result.Add(new GpuInfo(name, vendor, vram, shared, driver, isIntegrated));
        }
        return result;
    }

    private static GpuVendor ClassifyVendor(string? compatibility, string name)
    {
        var combined = ((compatibility ?? string.Empty) + " " + name).ToLowerInvariant();
        if (combined.Contains("nvidia")) return GpuVendor.Nvidia;
        if (combined.Contains("amd") || combined.Contains("radeon") || combined.Contains("advanced micro"))
            return GpuVendor.Amd;
        if (combined.Contains("intel")) return GpuVendor.Intel;
        if (combined.Contains("qualcomm") || combined.Contains("adreno")) return GpuVendor.Qualcomm;
        if (combined.Contains("microsoft")) return GpuVendor.Microsoft;
        return GpuVendor.Unknown;
    }

    private static bool ClassifyIntegrated(string name, GpuVendor vendor, long? vramBytes)
    {
        var n = name.ToLowerInvariant();

        // Strong indicators of dedicated GPU (return false early).
        if (n.Contains("geforce") || n.Contains("quadro") || n.Contains("rtx") || n.Contains("gtx"))
            return false;
        if (n.Contains("radeon rx") || n.Contains("radeon pro")) return false;
        if (n.Contains("arc a")) return false; // Intel Arc A-series dGPU

        // Strong indicators of integrated GPU.
        if (n.Contains("uhd graphics") || n.Contains("iris xe") || n.Contains("iris plus")) return true;
        if (n.Contains("hd graphics") && vendor == GpuVendor.Intel) return true;
        if (n.Contains("radeon graphics") && vendor == GpuVendor.Amd) return true;
        if (n.Contains("vega") && n.Contains("graphics")) return true; // Ryzen APU
        if (vendor == GpuVendor.Microsoft) return true; // "Microsoft Basic Display Adapter" fallback driver

        // Heuristic fallback: very small VRAM => treat as integrated.
        if (vramBytes is long vb && vb < 2L * 1024 * 1024 * 1024) return true;

        return false;
    }

    private const string DisplayClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private static Dictionary<string, long> ReadRegistryVram()
    {
        // Each numbered subkey under the display class is a video adapter. Modern Windows
        // exposes HardwareInformation.qwMemorySize (REG_QWORD) which is not capped at 4 GB
        // the way the legacy MemorySize (REG_DWORD) is.
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var classKey = baseKey.OpenSubKey(DisplayClassKey);
            if (classKey == null) return map;

            foreach (var subKeyName in classKey.GetSubKeyNames())
            {
                if (subKeyName.Length != 4 || !int.TryParse(subKeyName, out _))
                {
                    // Skip "Properties", "Configuration" etc; only numbered adapter keys.
                    continue;
                }

                using var subKey = classKey.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var adapterName = ReadAdapterString(subKey);
                if (string.IsNullOrWhiteSpace(adapterName)) continue;

                long? vram = ReadQwordVram(subKey);
                if (vram is long v && v > 0)
                {
                    map[adapterName] = v;
                }
            }
        }
        catch
        {
            // Registry read can be denied or driver entries malformed; tolerate.
        }
        return map;
    }

    private static string? ReadAdapterString(RegistryKey subKey)
    {
        // Try REG_SZ first; fall back to legacy REG_BINARY (UTF-16LE) and DriverDesc.
        var raw = subKey.GetValue("HardwareInformation.AdapterString");
        switch (raw)
        {
            case string s when !string.IsNullOrWhiteSpace(s):
                return s.Trim();
            case byte[] bytes when bytes.Length > 0:
            {
                var decoded = Encoding.Unicode.GetString(bytes).TrimEnd('\0').Trim();
                if (!string.IsNullOrWhiteSpace(decoded)) return decoded;
                break;
            }
        }
        if (subKey.GetValue("DriverDesc") is string desc && !string.IsNullOrWhiteSpace(desc))
        {
            return desc.Trim();
        }
        return null;
    }

    private static long? ReadQwordVram(RegistryKey subKey)
    {
        var raw = subKey.GetValue("HardwareInformation.qwMemorySize");
        switch (raw)
        {
            case long q when q > 0: return q;
            case byte[] bytes when bytes.Length >= 8: return BitConverter.ToInt64(bytes, 0);
        }
        // Legacy MemorySize (REG_DWORD, capped at 4 GB on older drivers).
        var legacy = subKey.GetValue("HardwareInformation.MemorySize");
        switch (legacy)
        {
            case int li when li > 0: return li;
            case byte[] lb when lb.Length >= 4: return BitConverter.ToInt32(lb, 0);
        }
        return null;
    }

    private static long? MatchRegistryVram(string adapterName, IReadOnlyDictionary<string, long> map)
    {
        if (map.TryGetValue(adapterName, out var exact)) return exact;

        // Loose match: registry adapter string sometimes carries trailing model qualifiers
        // ("NVIDIA GeForce RTX 4060" vs "NVIDIA GeForce RTX 4060 (Notebook)") that differ
        // slightly from the WMI Name.
        foreach (var kvp in map)
        {
            if (adapterName.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.StartsWith(adapterName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ NPU

    private static IReadOnlyList<NpuInfo> ProbeNpus()
    {
        var list = new List<NpuInfo>();
        // WQL note: parenthesised AND/NOT LIKE combinations are unreliable across
        // WMI providers (observed "Invalid query" on Windows 11 24H2). We keep the
        // WHERE clause to plain OR-of-LIKE and apply the ambiguous-name filter in C#.
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, Manufacturer FROM Win32_PnPEntity " +
            "WHERE Name LIKE '%AI Boost%' " +
            "OR Name LIKE '%Neural Processor%' " +
            "OR Name LIKE '%Neural Processing%' " +
            "OR Name LIKE '%Hexagon%' " +
            "OR Name LIKE '%XDNA%' " +
            "OR Name LIKE '%Ryzen AI%'");
        foreach (var obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;
            var vendor = ClassifyNpuVendor(name, manufacturer);
            if (vendor == NpuVendor.Unknown && !LooksLikeAccelerator(name))
            {
                // Skip ambiguous matches (e.g. random "NPU" branded peripherals) when we cannot
                // tie them back to a known accelerator vendor.
                continue;
            }
            list.Add(new NpuInfo(name, vendor));
        }
        return list;
    }

    private static bool LooksLikeAccelerator(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("ai") || n.Contains("neural") || n.Contains("hexagon")
               || n.Contains("xdna") || n.Contains("ipu");
    }

    private static NpuVendor ClassifyNpuVendor(string name, string manufacturer)
    {
        var combined = (name + " " + manufacturer).ToLowerInvariant();
        if (combined.Contains("ai boost") ||
            (combined.Contains("intel") && (combined.Contains("npu") || combined.Contains("neural"))))
        {
            return NpuVendor.IntelAiBoost;
        }
        if ((combined.Contains("amd") || combined.Contains("advanced micro")) &&
            (combined.Contains("ipu") || combined.Contains("xdna") || combined.Contains("npu") || combined.Contains("ryzen ai")))
        {
            return NpuVendor.AmdXdna;
        }
        if (combined.Contains("hexagon") ||
            (combined.Contains("qualcomm") && (combined.Contains("npu") || combined.Contains("neural"))))
        {
            return NpuVendor.QualcommHexagon;
        }
        return NpuVendor.Unknown;
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessorFeaturePresent(uint feature);

    public const uint PF_AVX2_INSTRUCTIONS_AVAILABLE = 40;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public sealed class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
