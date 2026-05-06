using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InteractiveMask.WebHost;

/// <summary>
/// Subset of the shared <c>config.json</c> that the WebHost cares about.
/// The full file is owned by Display.exe; we read it as a snapshot via
/// <see cref="WebSettingsProvider"/>.
/// </summary>
public sealed record WebSettings(
    string Language,
    int HttpPort,
    int? HttpsPort,
    string? CertPath,
    string? CertPassword,
    bool BindAllInterfaces)
{
    public static WebSettings Default { get; } = new("nl", 8080, null, null, null, false);
}

/// <summary>
/// Reads the shared <c>%PROGRAMDATA%\InteractiveMask\config.json</c> on demand,
/// caches the parsed result and re-reads only when the file's mtime changes so
/// changes saved by Display.exe surface in the WebHost without restart (for
/// fields that the WebHost can apply at runtime — Kestrel bindings still need
/// a restart since they are configured during host build).
/// </summary>
public sealed class WebSettingsProvider
{
    private readonly string _path;
    private readonly object _lock = new();
    private DateTime _lastReadUtc;
    private WebSettings _cached = WebSettings.Default;

    public WebSettingsProvider()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InteractiveMask", "config.json");
    }

    public WebSettings Current
    {
        get
        {
            lock (_lock)
            {
                try
                {
                    var info = new FileInfo(_path);
                    if (!info.Exists) return _cached;
                    if (info.LastWriteTimeUtc <= _lastReadUtc) return _cached;
                    _cached = ReadFromDisk();
                    _lastReadUtc = info.LastWriteTimeUtc;
                }
                catch
                {
                    // Last good cache wins; partial writes during a Display save
                    // resolve themselves on the next read.
                }
                return _cached;
            }
        }
    }

    /// <summary>
    /// Synchronously load settings once at host startup, before <see cref="Current"/>
    /// is wired into DI. Used by Kestrel configuration which needs concrete
    /// listener endpoints before <c>app.Run()</c>.
    /// </summary>
    public static WebSettings LoadOnce()
    {
        var provider = new WebSettingsProvider();
        return provider.Current;
    }

    private WebSettings ReadFromDisk()
    {
        using var stream = File.OpenRead(_path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        string lang = "nl";
        if (root.TryGetProperty("Language", out var langEl) && langEl.ValueKind == JsonValueKind.String)
        {
            lang = langEl.GetString() ?? "nl";
        }

        int httpPort = 8080;
        int? httpsPort = null;
        string? certPath = null;
        string? certPwd = null;
        bool bindAll = false;

        if (root.TryGetProperty("Web", out var web) && web.ValueKind == JsonValueKind.Object)
        {
            if (web.TryGetProperty("HttpPort", out var p)  && p.TryGetInt32(out var hp)) httpPort = hp;
            if (web.TryGetProperty("HttpsPort", out var hp2) && hp2.ValueKind == JsonValueKind.Number) httpsPort = hp2.GetInt32();
            if (web.TryGetProperty("CertPath",  out var cp)  && cp.ValueKind == JsonValueKind.String) certPath = cp.GetString();
            if (web.TryGetProperty("CertPasswordEncrypted", out var cpw) && cpw.ValueKind == JsonValueKind.String)
            {
                certPwd = DecryptPassword(cpw.GetString());
            }
            if (web.TryGetProperty("BindAllInterfaces", out var ba) && ba.ValueKind != JsonValueKind.Undefined)
            {
                bindAll = ba.ValueKind == JsonValueKind.True;
            }
        }

        return new WebSettings(lang, httpPort, httpsPort, certPath, certPwd, bindAll);
    }

    private static string? DecryptPassword(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            if (!OperatingSystem.IsWindows()) return null;
            var encrypted = Convert.FromBase64String(base64);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }
}
