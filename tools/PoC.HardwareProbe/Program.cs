using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
