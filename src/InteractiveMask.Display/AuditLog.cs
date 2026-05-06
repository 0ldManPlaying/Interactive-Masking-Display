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

    public string Path => _path;

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
    }
}
