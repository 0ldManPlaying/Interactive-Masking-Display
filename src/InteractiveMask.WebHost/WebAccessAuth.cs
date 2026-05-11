using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace InteractiveMask.WebHost;

/// <summary>
/// In-memory session store for the web-page authentication. One token per
/// successful login, sliding-window expiry. Loses sessions on service restart
/// (acceptable: user logs in again — we deliberately avoid a persistent store
/// so an admin who disables auth doesn't leave dangling sessions on disk).
/// <para>
/// Expired entries are normally only removed on the next <see cref="TryRefresh"/>
/// for that specific token. Tokens that no client ever re-presents (the user
/// closed the tab, the cookie expired client-side) would otherwise linger
/// indefinitely; a periodic sweep evicts them. Registered as a DI singleton,
/// so the runtime disposes the timer at service shutdown.
/// </para>
/// </summary>
public sealed class WebAccessSessionStore : IDisposable
{
    public const string CookieName = "im_session";
    public static TimeSpan DefaultLifetime { get; } = TimeSpan.FromHours(8);

    /// <summary>
    /// How often the background sweep evicts expired entries. Decoupled from
    /// the cookie TTL — sweeping more often than every few minutes is wasted
    /// work, sweeping rarely is fine because each entry is at most a tiny
    /// record (subject + DateTime).
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly Timer _sweepTimer;
    private int _disposed;

    public WebAccessSessionStore()
    {
        // Start the timer at construction so the sweep cadence is independent
        // of when the first session is issued. dueTime == period so the first
        // tick is one interval out (no need to sweep an empty dictionary at
        // startup).
        _sweepTimer = new Timer(_ => SweepExpired(), state: null,
            dueTime: SweepInterval, period: SweepInterval);
    }

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

    /// <summary>
    /// Sweep expired entries. ConcurrentDictionary iteration is safe to do
    /// while other threads add / remove entries; the snapshot enumerator
    /// reflects entries that exist at the time each element is visited.
    /// </summary>
    private void SweepExpired()
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        var nowUtc = DateTime.UtcNow;
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.ExpiryUtc <= nowUtc)
            {
                // TryRemove with the value-overload avoids the race where the
                // entry has just been refreshed by an authenticated request
                // between our check and the remove.
                _sessions.TryRemove(new KeyValuePair<string, SessionEntry>(kvp.Key, kvp.Value));
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _sweepTimer.Dispose();
    }

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
