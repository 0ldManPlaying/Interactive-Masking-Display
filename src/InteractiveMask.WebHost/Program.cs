using InteractiveMask.WebHost;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(opt => opt.ServiceName = "InteractiveMaskWebHost");

// Pull HTTP/HTTPS port + cert from the shared config.json; fall back to
// localhost:8080 when the config doesn't exist yet (first boot before Display
// has saved anything). Kestrel listens are decided here at host build time, so
// changing them requires a service restart.
var bootSettings = WebSettingsProvider.LoadOnce();
builder.WebHost.ConfigureKestrel(options =>
{
    var bindIp = bootSettings.BindAllInterfaces ? IPAddress.Any : IPAddress.Loopback;
    options.Listen(bindIp, bootSettings.HttpPort);

    if (bootSettings.HttpsPort is { } httpsPort &&
        httpsPort > 0 &&
        !string.IsNullOrEmpty(bootSettings.CertPath) &&
        File.Exists(bootSettings.CertPath))
    {
        try
        {
            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                bootSettings.CertPath,
                bootSettings.CertPassword ?? "",
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
            options.Listen(bindIp, httpsPort, listen => listen.UseHttps(cert));
        }
        catch
        {
            // Bad cert / wrong password ⇒ HTTP-only fallback. Logged via the
            // standard ASP.NET startup error stream when a request hits HTTPS.
        }
    }
});

builder.Services.AddRazorPages();
builder.Services.AddSingleton<StateMirror>();
builder.Services.AddSingleton<IpcCommandSender>();
builder.Services.AddSingleton<WebSettingsProvider>();
builder.Services.AddHostedService<IpcMirrorService>();

var app = builder.Build();

// Replace Kestrel's default stacktrace dump with a single readable line so a
// port collision (very common during dev: Apache/IIS on 8080, etc.) doesn't
// look like a serious crash. Any other failure still propagates.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    if (e.ExceptionObject is SocketException se && se.SocketErrorCode == SocketError.AccessDenied)
    {
        var port = bootSettings.HttpPort;
        Console.Error.WriteLine();
        Console.Error.WriteLine($"  Kan niet binden op poort {port}: een andere applicatie luistert daar al,");
        Console.Error.WriteLine($"  of Windows heeft de poort gereserveerd.");
        Console.Error.WriteLine($"  Tip: verander de HTTP-poort in Display -> Setup -> Web-UI,");
        Console.Error.WriteLine($"       of stop het andere proces (bv. Apache, IIS, andere WebHost-instantie).");
        Console.Error.WriteLine($"  Gebruik 'netstat -ano | findstr :{port}' om de bezetter te vinden.");
        Console.Error.WriteLine();
        Environment.Exit(2);
    }
};

app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/api/state", (StateMirror mirror) => mirror.Snapshot());

app.MapPost("/api/toggle", async (HttpContext ctx, ToggleApiRequest body, IpcCommandSender sender, WebSettingsProvider settings, CancellationToken ct) =>
{
    var t = Translations.For(settings.Current.Language);
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var source = $"web:{ip}";
    try
    {
        var resp = await sender.ToggleAsync(body.Slot, body.Pin, source, ct);
        return Results.Ok(new
        {
            result = resp.Result.ToString(),
            lockoutSecondsRemaining = resp.LockoutSecondsRemaining,
        });
    }
    catch (InvalidOperationException)
    {
        return Results.Json(new { result = "IpcError", detail = t.IpcUnavailable }, statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        return Results.Json(new { result = "IpcError", detail = t.IpcTimeout }, statusCode: 504);
    }
});

// Read-only audit-log tail. Reads the NDJSON file written by Display.exe directly.
// Used for diagnostic during dev; the production setup wizard will have a richer
// viewer with filters + CSV export.
app.MapGet("/api/audit", (int? limit) =>
{
    var path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "InteractiveMask", "audit.log");
    if (!File.Exists(path)) return Results.Ok(Array.Empty<object>());

    int max = Math.Clamp(limit ?? 100, 1, 5000);
    // Tail: read all lines (the file is small in practice; rotation comes later).
    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = new StreamReader(fs);
    var lines = new List<string>();
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        if (line.Length > 0) lines.Add(line);
    }
    var tail = lines.Skip(Math.Max(0, lines.Count - max));
    var events = tail.Select(l =>
    {
        try { return JsonSerializer.Deserialize<JsonElement>(l); }
        catch { return default; }
    }).Where(e => e.ValueKind != JsonValueKind.Undefined).ToList();
    return Results.Ok(events);
});

app.Run();

internal sealed record ToggleApiRequest(int Slot, string? Pin);
