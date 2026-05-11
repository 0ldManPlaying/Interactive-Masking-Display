using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveMask.Display;

public enum AuditEventType
{
    MaskOn,
    MaskOff,
    MaskAutoReset,
    PinSet,
    PinFail,
    PinLockout,
    NvrConnected,
    NvrDisconnected,
    AppStarted,
    AppStopped,
    /// <summary>
    /// Mass-mask gesture (long-press): every tile transitions to the masked
    /// state in a single action. Detail carries the count of tiles touched.
    /// </summary>
    MassMaskOn,
    /// <summary>
    /// Mass-unmask gesture (long-press): every tile is released from the
    /// masked state at once. Auth follows the existing per-tile policy and
    /// runs once, before this event is written.
    /// </summary>
    MassMaskOff,
    /// <summary>
    /// v2.0: AI detector finished initialising and is Ready. Detail carries the
    /// backend (DirectML / CPU) plus active model name; one entry per session
    /// after a successful InitializeAsync.
    /// </summary>
    AiDetectorInit,
    /// <summary>
    /// v2.0: AI detector init failed or a non-recoverable runtime fault was
    /// observed. Detail carries the exception type + message. The Display
    /// continues with v1.x masking only after this event.
    /// </summary>
    AiDetectorFault,
    /// <summary>
    /// v2.0: AI detector dispose completed; one entry on graceful shutdown.
    /// Useful for support to confirm the detector got a clean tear-down
    /// instead of being killed via Task Manager or a crash.
    /// </summary>
    AiDetectorStopped,
    /// <summary>
    /// v2.0.x: an authorised reviewer suppressed the AI-mask overlay on one
    /// tile for a bounded duration. Detail carries the duration in seconds
    /// (or "indefinite" for the "until I re-mask" choice). Source carries
    /// the reviewer identity: an AD username when the session is in AD-mode,
    /// or "session-pin" / "desktop" otherwise. Camera content is fully
    /// visible during the reveal window; the underlying NVR recording is
    /// unaffected. Per-detection reveal is intentionally not supported.
    /// </summary>
    AiRevealRequested,
    /// <summary>
    /// v2.0.x: an active AI-reveal window ended. Detail carries the reason:
    /// "timer-expired" (auto-restore), "manual-remask" (reviewer pressed the
    /// remask badge), "mass-mask" (long-press cancelled all reveals), or
    /// "tile-rebound" (camera binding changed in Setup → Apply).
    /// </summary>
    AiRevealExpired,
}

/// <summary>One row in the audit log. Serialised as a JSON object on a single line.</summary>
public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    AuditEventType Type,
    int? Slot,
    int? CameraIndex,
    string? Label,
    string? Source,
    string? Detail);

/// <summary>
/// Append-only NDJSON audit log shared between Display and WebHost. Lives in
/// <c>%PROGRAMDATA%\InteractiveMask\audit.log</c> so the Windows-Service-hosted
/// WebHost can read it for the upcoming <c>/api/audit</c> endpoint without
/// needing IPC plumbing for queries.
///
/// One event per line; we append synchronously to keep ordering deterministic
/// and so a hard crash during a privacy-related action doesn't lose the event.
/// </summary>
public sealed class AuditLog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _lock = new();
    private readonly string _path;
    private IAuditForwarder? _forwarder;

    public string Path => _path;

    /// <summary>
    /// Attach (or replace) the live-forwarder. Pass null to detach. The previous
    /// forwarder is disposed by the caller; AuditLog itself never owns the
    /// forwarder lifetime so settings can hot-swap without log gaps.
    /// </summary>
    public void SetForwarder(IAuditForwarder? forwarder) => _forwarder = forwarder;

    public AuditLog()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InteractiveMask");
        try { Directory.CreateDirectory(dir); } catch { }
        _path = System.IO.Path.Combine(dir, "audit.log");
    }

    public void Write(AuditEventType type, int? slot = null, int? cameraIndex = null,
                      string? label = null, string? source = null, string? detail = null)
    {
        var ev = new AuditEvent(
            Timestamp: DateTimeOffset.Now,
            Type: type,
            Slot: slot,
            CameraIndex: cameraIndex,
            Label: label,
            Source: source,
            Detail: detail);

        try
        {
            var json = JsonSerializer.Serialize(ev, JsonOpts);
            lock (_lock)
            {
                File.AppendAllText(_path, json + Environment.NewLine);
            }
        }
        catch
        {
            // Audit must never crash the app. A failed write is observed via
            // missing entries when reviewing the log; in production we'd add a
            // fallback to the Windows Event Log.
        }

        // Forward after the local write so a slow sink can't delay the disk
        // write; the forwarder itself is non-blocking (bounded queue).
        try { _forwarder?.Forward(ev); } catch { /* sink failures must never crash audit */ }
    }
}
