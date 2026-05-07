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
    bool BindAllInterfaces,
    /// <summary>Per-tile unmask auth flow: "pin" (default), "ad", or "off".</summary>
    string AuthMode,
    /// <summary>AD-mode default domain pre-filled in the credentials prompt; empty = none.</summary>
    string AuthDomain,
    /// <summary>Web-page access auth (gates the whole UI): "off", "pin", or "ad".</summary>
    string AccessMode,
    /// <summary>Decrypted web-access PIN. Only meaningful when AccessMode = "pin".</summary>
    string? AccessPin,
    /// <summary>Default domain for the AD login form. Only used when AccessMode = "ad".</summary>
    string AccessDomain)
{
    public static WebSettings Default { get; } = new("nl", 8080, null, null, null, false, "pin", "", "off", null, "");
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

    /// <summary>
    /// Fires when the on-disk access-auth settings (mode, PIN, domain) change
    /// since the previous cached read. Subscribers (e.g. <c>WebAccessSessionStore</c>)
    /// can revoke active cookies so a freshly disabled mode or a rotated PIN
    /// invalidates outstanding sessions.
    /// </summary>
    public event Action? AccessChanged;

    public WebSettings Current
    {
        get
        {
            bool fireAccessChanged = false;
            lock (_lock)
            {
                try
                {
                    var info = new FileInfo(_path);
                    if (!info.Exists) return _cached;
                    if (info.LastWriteTimeUtc <= _lastReadUtc) return _cached;
                    var previous = _cached;
                    _cached = ReadFromDisk();
                    _lastReadUtc = info.LastWriteTimeUtc;
                    if (previous.AccessMode != _cached.AccessMode ||
                        previous.AccessPin != _cached.AccessPin ||
                        previous.AccessDomain != _cached.AccessDomain)
                    {
                        fireAccessChanged = true;
                    }
                }
                catch
                {
                    // Last good cache wins; partial writes during a Display save
                    // resolve themselves on the next read.
                }
            }
            // Fire OUTSIDE the lock so handlers can call back into the provider
            // (or take their own locks) without risking a deadlock.
            if (fireAccessChanged) AccessChanged?.Invoke();
            return _cached;
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

        string accessMode = "off";
        string? accessPin = null;
        string accessDomain = "";

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
            if (web.TryGetProperty("Access", out var acc) && acc.ValueKind == JsonValueKind.Object)
            {
                if (acc.TryGetProperty("Mode", out var m) && m.ValueKind == JsonValueKind.String) accessMode = m.GetString() ?? "off";
                if (acc.TryGetProperty("PinEncrypted", out var pe) && pe.ValueKind == JsonValueKind.String) accessPin = DecryptPassword(pe.GetString());
                if (acc.TryGetProperty("Domain", out var ad) && ad.ValueKind == JsonValueKind.String) accessDomain = ad.GetString() ?? "";
            }
        }

        // Mirror Display's AuthSettings + PrivacySettings.RequireSessionPin so the
        // browser shows the right modal: AD-creds in AD mode, PIN-keypad otherwise,
        // nothing at all when auth is fully disabled.
        string authMode = "pin";
        string authDomain = "";
        bool useAd = false;
        if (root.TryGetProperty("Auth", out var auth) && auth.ValueKind == JsonValueKind.Object)
        {
            if (auth.TryGetProperty("UseActiveDirectory", out var u) && u.ValueKind != JsonValueKind.Undefined)
            {
                useAd = u.ValueKind == JsonValueKind.True;
            }
            if (auth.TryGetProperty("Domain", out var d) && d.ValueKind == JsonValueKind.String)
            {
                authDomain = d.GetString() ?? "";
            }
        }
        bool requirePin = true;
        if (root.TryGetProperty("Privacy", out var priv) && priv.ValueKind == JsonValueKind.Object)
        {
            if (priv.TryGetProperty("RequireSessionPin", out var r) && r.ValueKind != JsonValueKind.Undefined)
            {
                requirePin = r.ValueKind != JsonValueKind.False;
            }
        }
        if (useAd) authMode = "ad";
        else if (!requirePin) authMode = "off";

        return new WebSettings(lang, httpPort, httpsPort, certPath, certPwd, bindAll, authMode, authDomain, accessMode, accessPin, accessDomain);
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
