using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using InteractiveMask.Detection;
using InteractiveMask.Hardware;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("InteractiveMask Hardware Probe (v2.0 prerequisite work P2)");
Console.WriteLine("==========================================================");
Console.WriteLine();

var sw = Stopwatch.StartNew();
var profile = HostCapabilityProbe.Probe();
sw.Stop();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};
var json = JsonSerializer.Serialize(profile, jsonOptions);

Console.WriteLine(json);
Console.WriteLine();
Console.WriteLine($"Probe completed in {sw.ElapsedMilliseconds} ms.");
Console.WriteLine($"Resolved tier: {profile.Tier}");

if (profile.ProbeWarnings.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Warnings:");
    foreach (var warning in profile.ProbeWarnings)
    {
        Console.WriteLine($"  - {warning}");
    }
}

// Optional: write JSON next to the executable for inspection.
var outPath = Path.Combine(AppContext.BaseDirectory, "capability-profile.json");
File.WriteAllText(outPath, json);
Console.WriteLine();
Console.WriteLine($"Profile written to: {outPath}");

// ------------------------------------------------------------------
// Benchmark smoke-test (P3 deliverable)
// ------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("Running benchmark...");
using var runner = new BenchmarkRunner(BenchmarkOptions.Quick);
try
{
    var progress = new Progress<BenchmarkProgress>(p =>
    {
        if (p.Completed % 10 == 0 || p.Completed == p.Total)
        {
            Console.Write($"\r  {p.Completed}/{p.Total} frames");
        }
    });
    var benchSw = Stopwatch.StartNew();
    var bench = await runner.RunAsync(progress);
    benchSw.Stop();
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"  Execution provider: {bench.ExecutionProvider}");
    Console.WriteLine($"  Model:              {bench.ModelFileName} ({string.Join("x", bench.InputDimensions)})");
    Console.WriteLine($"  Cold start:         {bench.ColdStartMs:0.00} ms");
    Console.WriteLine($"  Steady-state p50:   {bench.SteadyStateP50Ms:0.00} ms");
    Console.WriteLine($"  Steady-state p95:   {bench.SteadyStateP95Ms:0.00} ms");
    Console.WriteLine($"  Steady-state p99:   {bench.SteadyStateP99Ms:0.00} ms");
    Console.WriteLine($"  Throughput:         {bench.ThroughputFps:0.0} fps");
    Console.WriteLine($"  Wall time:          {benchSw.Elapsed.TotalSeconds:0.0} s");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"Benchmark failed: {ex.GetType().Name}: {ex.Message}");
}

// ------------------------------------------------------------------
// NullDetector smoke test (P4 deliverable)
// ------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("NullDetector smoke test...");
try
{
    await using var nullDetector = new NullDetector();
    var config = new DetectorConfig(
        EnabledClasses: new HashSet<ObjectClass> { ObjectClass.Person, ObjectClass.TwoWheeler, ObjectClass.Vehicle },
        ConfidenceThresholds: new Dictionary<ObjectClass, float>
        {
            [ObjectClass.Person]     = 0.4f,
            [ObjectClass.TwoWheeler] = 0.4f,
            [ObjectClass.Vehicle]    = 0.4f,
        },
        MaxQueueDepth: 1,
        PreferPolygonMasks: false);
    await nullDetector.InitializeAsync(config);

    Console.WriteLine($"  Backend:           {nullDetector.Capability.BackendName}");
    Console.WriteLine($"  Status:            {nullDetector.Status}");
    Console.WriteLine($"  Supported classes: {nullDetector.Capability.SupportedClasses.Count} (expected 0)");

    // Submit a dummy frame and confirm the detector returns an empty set
    // (the contract guarantees full-tile blur fallback on the render side).
    var dummyFrame = new DummyFrame(TimestampTicks: DateTime.UtcNow.Ticks, Width: 1920, Height: 1080, StreamId: 0);
    var detectionFrame = await nullDetector.DetectAsync(dummyFrame);
    Console.WriteLine($"  Returned detects:  {detectionFrame.Detections.Count} (expected 0)");
    Console.WriteLine($"  Frame timestamp:   {detectionFrame.FrameTimestampTicks} (matches: {detectionFrame.FrameTimestampTicks == dummyFrame.TimestampTicks})");
}
catch (Exception ex)
{
    Console.WriteLine($"NullDetector smoke test failed: {ex.GetType().Name}: {ex.Message}");
}

// ------------------------------------------------------------------
// OnnxLocalDetector smoke test (M2: YOLOv8n COCO multi-class)
// ------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("OnnxLocalDetector smoke test (YOLOv8n COCO Person / TwoWheeler / Vehicle)...");
try
{
    await using var detector = new OnnxLocalDetector();
    var detectorConfig = new DetectorConfig(
        EnabledClasses: new HashSet<ObjectClass>
        {
            ObjectClass.Person,
            ObjectClass.TwoWheeler,
            ObjectClass.Vehicle,
        },
        ConfidenceThresholds: new Dictionary<ObjectClass, float>
        {
            [ObjectClass.Person]     = 0.4f,
            [ObjectClass.TwoWheeler] = 0.4f,
            [ObjectClass.Vehicle]    = 0.4f,
        },
        MaxQueueDepth: 1,
        PreferPolygonMasks: false);

    var initSw = Stopwatch.StartNew();
    await detector.InitializeAsync(detectorConfig);
    initSw.Stop();
    Console.WriteLine($"  Init: {detector.Capability.ModelDescription} in {initSw.ElapsedMilliseconds} ms");
    Console.WriteLine($"  Status: {detector.Status}");

    // Synthesise a 1920x1080 BGRA8 frame filled with random noise. YOLOv8n should
    // return zero (or near-zero) detections on pure noise; we are validating the
    // pipeline (preprocess, inference, decode, NMS) end-to-end, not accuracy.
    const int testWidth = 1920;
    const int testHeight = 1080;
    var bgra = new byte[testWidth * testHeight * 4];
    var detRng = new Random(7);
    detRng.NextBytes(bgra);
    var testFrame = BitmapFrameRef.FromBgra(
        timestampTicks: DateTime.UtcNow.Ticks,
        width: testWidth,
        height: testHeight,
        streamId: 0,
        bgraPixels: bgra);

    // Run two passes: first triggers ORT kernel compilation, second is steady-state.
    var detection0 = await detector.DetectAsync(testFrame);
    var detection1 = await detector.DetectAsync(testFrame);
    Console.WriteLine($"  Cold pass:    {detection0.Metrics.InferenceLatencyMs:0.0} ms, {detection0.Detections.Count} detections");
    Console.WriteLine($"  Warm pass:    {detection1.Metrics.InferenceLatencyMs:0.0} ms, {detection1.Detections.Count} detections");
    Console.WriteLine($"  Frame ts:     {detection1.FrameTimestampTicks} (matches: {detection1.FrameTimestampTicks == testFrame.TimestampTicks})");
}
catch (Exception ex)
{
    Console.WriteLine($"  OnnxLocalDetector smoke test failed: {ex.GetType().Name}: {ex.Message}");
}

// Local FrameRef subtype used only by this smoke test; real backends will define
// their own typed FrameRef holding decoded-bitmap or GPU-resource handles.
internal sealed record DummyFrame(long TimestampTicks, int Width, int Height, int StreamId)
    : FrameRef(TimestampTicks, Width, Height, StreamId);
