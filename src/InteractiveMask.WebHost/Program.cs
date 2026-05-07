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
builder.Services.AddSingleton<WebAccessSessionStore>();
builder.Services.AddHostedService<IpcMirrorService>();

var app = builder.Build();

// Revoke every active web-access session as soon as the admin changes the
// access mode / PIN / domain on disk. Without this, a previously-issued
// HttpOnly cookie keeps unlocking the UI for up to 8 hours after a PIN
// rotation — exactly the gap the rotation is supposed to close.
{
    var settingsForWiring = app.Services.GetRequiredService<WebSettingsProvider>();
    var sessionsForWiring = app.Services.GetRequiredService<WebAccessSessionStore>();
    settingsForWiring.AccessChanged += () => sessionsForWiring.RevokeAll();
}

// Replace Kestrel's default stacktrace dump with a single readable line so a
// port collision (very common during dev: Apache/IIS on 8080, etc.) doesn't
// look like a serious crash. Any other failure still propagates.
// Walk inner exceptions because Kestrel wraps the SocketException in an
// IOException (and AddressInUseException) before it reaches us.
static SocketException? FindSocket(Exception? ex)
{
    while (ex is not null)
    {
        if (ex is SocketException se) return se;
        ex = ex.InnerException;
    }
    return null;
}

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var se = e.ExceptionObject is Exception ex ? FindSocket(ex) : null;
    if (se is null) return;

    var port = bootSettings.HttpPort;
    var inUse = se.SocketErrorCode == SocketError.AddressAlreadyInUse;
    var denied = se.SocketErrorCode == SocketError.AccessDenied;
    if (!inUse && !denied) return;

    Console.Error.WriteLine();
    if (inUse)
    {
        Console.Error.WriteLine($"  Poort {port} is al in gebruik door een andere applicatie.");
        Console.Error.WriteLine($"  Veelvoorkomend: een eerdere WebHost-instantie draait nog, of Apache/IIS bezet de poort.");
    }
    else
    {
        Console.Error.WriteLine($"  Kan niet binden op poort {port}: Windows heeft de poort gereserveerd of er is geen permissie.");
    }
    Console.Error.WriteLine($"  Tip: stop het andere proces, of verander de HTTP-poort via Display -> Setup -> Web-UI.");
    Console.Error.WriteLine($"  Vinden welk proces de poort gebruikt: netstat -ano | findstr :{port}");
    Console.Error.WriteLine();
    Environment.Exit(2);
};

app.UseStaticFiles();
// Auth middleware runs after static files (CSS/JS for the login page must be
// reachable while unauthenticated) but before route handlers.
app.UseMiddleware<WebAccessAuthMiddleware>();
app.MapRazorPages();

// Tells the login page which mode to render. Always reachable (whitelisted in
// the auth middleware) so the page can boot even when the user isn't logged in.
app.MapGet("/api/access-mode", (WebSettingsProvider settings) => Results.Ok(new
{
    mode = settings.Current.AccessMode,
    domain = settings.Current.AccessDomain,
}));

app.MapPost("/api/login", async (HttpContext ctx, LoginRequest body, WebSettingsProvider settings, WebAccessSessionStore sessions) =>
{
    var current = settings.Current;
    var mode = current.AccessMode ?? "off";

    if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
    {
        // Auth disabled — issue a session anyway so the cookie path is uniform.
        CookieHelpers.IssueCookie(ctx, sessions, "anonymous");
        return Results.Ok(new { ok = true });
    }

    string subject;
    if (string.Equals(mode, "pin", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrEmpty(body.Pin) || string.IsNullOrEmpty(current.AccessPin))
        {
            return Results.Json(new { ok = false, error = "wrong" }, statusCode: 401);
        }
        // Constant-time comparison so a remote attacker can't probe the PIN length.
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(body.Pin),
                System.Text.Encoding.UTF8.GetBytes(current.AccessPin)))
        {
            return Results.Json(new { ok = false, error = "wrong" }, statusCode: 401);
        }
        subject = "pin";
    }
    else if (string.Equals(mode, "ad", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrEmpty(body.Username))
        {
            return Results.Json(new { ok = false, error = "wrong" }, statusCode: 401);
        }
        var auth = WindowsAuth.Authenticate(body.Username!, body.Password ?? "", current.AccessDomain);
        if (!auth.Success)
        {
            return Results.Json(new { ok = false, error = "wrong" }, statusCode: 401);
        }
        subject = auth.Username ?? body.Username!;
    }
    else
    {
        return Results.Json(new { ok = false, error = "unknown-mode" }, statusCode: 500);
    }

    CookieHelpers.IssueCookie(ctx, sessions, subject);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/logout", (HttpContext ctx, WebAccessSessionStore sessions) =>
{
    var token = ctx.Request.Cookies[WebAccessSessionStore.CookieName];
    sessions.Revoke(token);
    ctx.Response.Cookies.Delete(WebAccessSessionStore.CookieName);
    return Results.Ok(new { ok = true });
});
app.MapGet("/api/state", (StateMirror mirror) => mirror.Snapshot());

// Tells the browser which credential modal to render. Driven by Display's
// AuthSettings.UseActiveDirectory + PrivacySettings.RequireSessionPin in
// config.json; re-read on each request so live-apply changes propagate.
app.MapGet("/api/auth-mode", (WebSettingsProvider settings) => Results.Ok(new
{
    mode = settings.Current.AuthMode,
    domain = settings.Current.AuthDomain,
}));

app.MapPost("/api/toggle", async (HttpContext ctx, ToggleApiRequest body, IpcCommandSender sender, WebSettingsProvider settings, CancellationToken ct) =>
{
    var current = settings.Current;
    var t = Translations.For(current.Language);
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    string source = $"web:{ip}";
    bool preAuthenticated = false;
    string? pin = body.Pin;

    // AD mode: validate Windows credentials in-process before forwarding the
    // toggle. This keeps the Display side free of LogonUser calls and means
    // the user's password never travels over the IPC pipe.
    if (string.Equals(current.AuthMode, "ad", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrEmpty(body.Username))
    {
        var auth = WindowsAuth.Authenticate(body.Username!, body.Password ?? "", current.AuthDomain);
        if (!auth.Success)
        {
            return Results.Ok(new { result = InteractiveMask.Ipc.ToggleResult.CredentialsWrong.ToString() });
        }
        preAuthenticated = true;
        source = $"web:{ip}:user:{auth.Username}";
        pin = null;
    }

    try
    {
        var resp = await sender.ToggleAsync(body.Slot, pin, source, preAuthenticated, ct);
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

// One-shot JPEG snapshot for a tile. The Display side encodes from its current
// WriteableBitmap on the UI thread; we just relay the bytes. Returns:
//   200 + image/jpeg  on success
//   404                if the slot is empty / has no frame yet
//   403                if the tile is currently masked (privacy first)
//   503/504           IPC errors
app.MapGet("/api/snapshot/{slot:int}", async (int slot, IpcCommandSender sender, CancellationToken ct) =>
{
    try
    {
        var resp = await sender.SnapshotAsync(slot, ct);
        return resp.Status switch
        {
            InteractiveMask.Ipc.SnapshotStatus.Ok when resp.JpegBase64 is not null
                => Results.File(Convert.FromBase64String(resp.JpegBase64), "image/jpeg"),
            InteractiveMask.Ipc.SnapshotStatus.Masked   => Results.StatusCode(403),
            InteractiveMask.Ipc.SnapshotStatus.NoFrame  => Results.NotFound(),
            _                                            => Results.NotFound(),
        };
    }
    catch (InvalidOperationException) { return Results.StatusCode(503); }
    catch (TaskCanceledException) { return Results.StatusCode(504); }
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

internal sealed record ToggleApiRequest(int Slot, string? Pin, string? Username = null, string? Password = null);
internal sealed record LoginRequest(string? Pin, string? Username, string? Password);

internal static class CookieHelpers
{
    public static void IssueCookie(HttpContext ctx, WebAccessSessionStore sessions, string subject)
    {
        var token = sessions.Issue(subject);
        var opts = new CookieOptions
        {
            HttpOnly = true,
            // Secure flag matches the request scheme so the cookie also works
            // on plain-HTTP localhost during development.
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = WebAccessSessionStore.DefaultLifetime,
        };
        ctx.Response.Cookies.Append(WebAccessSessionStore.CookieName, token, opts);
    }
}
