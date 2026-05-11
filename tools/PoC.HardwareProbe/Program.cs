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
