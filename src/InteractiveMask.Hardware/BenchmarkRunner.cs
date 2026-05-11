using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace InteractiveMask.Hardware;

/// <summary>
/// Runs a fixed-workload ONNX inference benchmark and produces latency statistics
/// for the v2.0 capability tier. The benchmark model is MobileNetV2 (224x224)
/// from the ONNX Model Zoo; its compute profile is representative of YOLOv8n-class
/// detection workloads and gives an order-of-magnitude indication of what the
/// actual plate-detector model will achieve on the same host.
/// </summary>
public sealed class BenchmarkRunner : IDisposable
{
    public const string DefaultModelFileName = "mobilenetv2-7.onnx";

    private readonly BenchmarkOptions _options;
    private InferenceSession? _session;

    public BenchmarkRunner(BenchmarkOptions? options = null)
    {
        _options = options ?? BenchmarkOptions.Default;
    }

    /// <summary>
    /// Resolves the benchmark model path relative to the executable. Returns null
    /// if the model file is not present (e.g. dev-build without the asset copy).
    /// </summary>
    public static string? FindModelPath(string fileName = DefaultModelFileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "models", fileName),
            Path.Combine(Path.GetDirectoryName(typeof(BenchmarkRunner).Assembly.Location) ?? string.Empty, "models", fileName),
        };
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
        }
        return null;
    }

    /// <summary>
    /// Runs the benchmark. Throws <see cref="FileNotFoundException"/> if the
    /// benchmark model cannot be located. Throws <see cref="OperationCanceledException"/>
    /// if cancelled mid-run.
    /// </summary>
    public async Task<BenchmarkResult> RunAsync(
        IProgress<BenchmarkProgress>? progress = null,
        CancellationToken ct = default)
    {
        var modelPath = FindModelPath()
            ?? throw new FileNotFoundException(
                $"Benchmark model not found. Expected at {Path.Combine(AppContext.BaseDirectory, "models", DefaultModelFileName)}");

        // ONNX Runtime operations are CPU-blocking; offload to a worker thread so the
        // UI dispatcher stays responsive.
        return await Task.Run(() => RunCore(modelPath, progress, ct), ct).ConfigureAwait(false);
    }

    private BenchmarkResult RunCore(string modelPath, IProgress<BenchmarkProgress>? progress, CancellationToken ct)
    {
        // 1. Session creation. Prefer DirectML for broad GPU support; fall back to CPU EP.
        var (session, executionProvider) = CreateSession(modelPath);
        _session = session;

        // 2. Input shape resolution. Replace dynamic / batch dims with 1.
        var inputEntry = _session.InputMetadata.First();
        var inputName = inputEntry.Key;
        var dims = inputEntry.Value.Dimensions.ToArray();
        for (int i = 0; i < dims.Length; i++)
        {
            if (dims[i] < 1) dims[i] = i == 0 ? 1 : 224;
        }
        var elementCount = dims.Aggregate(1, (a, b) => a * b);

        // 3. Random-but-deterministic input data. We do not care about output correctness;
        //    only that inference runs the same arithmetic per host.
        var rng = new Random(42);
        var data = new float[elementCount];
        for (int i = 0; i < elementCount; i++) data[i] = (float)rng.NextDouble();

        var tensor = new DenseTensor<float>(data, dims);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        // 4. Cold-start measurement: first run carries JIT, kernel compilation and
        //    GPU upload overhead. Reported separately because it skews steady-state.
        ct.ThrowIfCancellationRequested();
        var coldSw = Stopwatch.StartNew();
        using (_session.Run(inputs)) { }
        coldSw.Stop();
        var coldStartMs = coldSw.Elapsed.TotalMilliseconds;

        // 5. Additional warmup runs after cold-start to reach steady state.
        for (int i = 0; i < _options.WarmupFrames; i++)
        {
            ct.ThrowIfCancellationRequested();
            using (_session.Run(inputs)) { }
        }

        // 6. Measured runs.
        var measurements = new double[_options.MeasurementFrames];
        var swPerFrame = new Stopwatch();
        var swTotal = Stopwatch.StartNew();
        for (int i = 0; i < _options.MeasurementFrames; i++)
        {
            ct.ThrowIfCancellationRequested();
            swPerFrame.Restart();
            using (_session.Run(inputs)) { }
            swPerFrame.Stop();
            measurements[i] = swPerFrame.Elapsed.TotalMilliseconds;

            if (progress != null && (i % 10 == 0 || i == _options.MeasurementFrames - 1))
            {
                progress.Report(new BenchmarkProgress(i + 1, _options.MeasurementFrames));
            }
        }
        swTotal.Stop();

        // 7. Statistics. Sorted-array percentiles with safe index clamping.
        Array.Sort(measurements);
        double Percentile(double p)
        {
            var idx = Math.Min(measurements.Length - 1, Math.Max(0, (int)Math.Ceiling(p * measurements.Length) - 1));
            return measurements[idx];
        }

        return new BenchmarkResult(
            RunAt: DateTime.UtcNow,
            ColdStartMs: coldStartMs,
            SteadyStateP50Ms: Percentile(0.50),
            SteadyStateP95Ms: Percentile(0.95),
            SteadyStateP99Ms: Percentile(0.99),
            ThroughputFps: _options.MeasurementFrames / swTotal.Elapsed.TotalSeconds,
            ExecutionProvider: executionProvider,
            ModelFileName: Path.GetFileName(modelPath),
            InputDimensions: dims);
    }

    private static (InferenceSession Session, string ProviderName) CreateSession(string modelPath)
    {
        // Try DirectML first; fall back to CPU if DirectML init fails (rare on
        // modern Windows but possible on Windows Server SKUs without DML).
        try
        {
            var opts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };
            // Device 0 = primary adapter as enumerated by DXGI.
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

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}

/// <summary>Configuration knobs for <see cref="BenchmarkRunner"/>.</summary>
public sealed record BenchmarkOptions(int WarmupFrames, int MeasurementFrames)
{
    public static BenchmarkOptions Default { get; } = new(WarmupFrames: 10, MeasurementFrames: 200);

    /// <summary>Short benchmark for development / smoke-tests.</summary>
    public static BenchmarkOptions Quick { get; } = new(WarmupFrames: 3, MeasurementFrames: 50);
}

/// <summary>Progress update emitted during a benchmark run.</summary>
public sealed record BenchmarkProgress(int Completed, int Total);
