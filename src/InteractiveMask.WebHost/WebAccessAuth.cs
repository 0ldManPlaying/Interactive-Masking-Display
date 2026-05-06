using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace InteractiveMask.WebHost;

/// <summary>
/// In-memory session store for the web-page authentication. One token per
/// successful login, sliding-window expiry. Loses sessions on service restart
/// (acceptable: user logs in again — we deliberately avoid a persistent store
/// so an admin who disables auth doesn't leave dangling sessions on disk).
/// </summary>
public sealed class WebAccessSessionStore
{
    public const string CookieName = "im_session";
    public static TimeSpan DefaultLifetime { get; } = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

    public string Issue(string subject)
    {
        var token = GenerateToken();
        _sessions[token] = new SessionEntry(subject, DateTime.UtcNow + DefaultLifetime);
        return token;
    }

    public bool TryRefresh(string token, out string subject)
    {
        subject = "";
        if (string.IsNullOrEmpty(token)) return false;
        if (!_sessions.TryGetValue(token, out var entry)) return false;
        if (entry.ExpiryUtc <= DateTime.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }
        // Sliding window: every authenticated request bumps the expiry.
        _sessions[token] = entry with { ExpiryUtc = DateTime.UtcNow + DefaultLifetime };
        subject = entry.Subject;
        return true;
    }

    public void Revoke(string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        _sessions.TryRemove(token, out _);
    }

    /// <summary>Wipe every active session. Called when the admin changes the
    /// access mode/PIN so previously-issued tokens stop working immediately.</summary>
    public void RevokeAll() => _sessions.Clear();

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private sealed record SessionEntry(string Subject, DateTime ExpiryUtc);
}

/// <summary>
/// Gates every route except /login, /api/login, /api/logout and static assets.
/// When access-mode is "off" the middleware short-circuits and lets the request
/// through so the open-mode behaviour is unchanged.
/// </summary>
public sealed class WebAccessAuthMiddleware
{
    private readonly RequestDelegate _next;

    public WebAccessAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, WebSettingsProvider settings, WebAccessSessionStore sessions)
    {
        var mode = settings.Current.AccessMode;
        if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        if (IsAlwaysAllowed(path))
        {
            await _next(ctx);
            return;
        }

        var token = ctx.Request.Cookies[WebAccessSessionStore.CookieName];
        if (!string.IsNullOrEmpty(token) && sessions.TryRefresh(token, out var subject))
        {
            ctx.Items["WebSubject"] = subject;
            await _next(ctx);
            return;
        }

        // For API routes: 401 JSON. For pages: 302 to /login.
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "auth-required" });
            return;
        }

        var returnUrl = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
        ctx.Response.Redirect($"/login?returnUrl={returnUrl}");
    }

    private static bool IsAlwaysAllowed(string path) =>
        path.Equals("/login", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/login", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/logout", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/access-mode", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
}
