using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveMask.Ipc;

public enum IpcMessageType
{
    /// <summary>Server -> client. Initial snapshot on connect, full grid + tile states.</summary>
    Hello = 1,
    /// <summary>Server -> client. One tile changed (mask, status, countdown).</summary>
    TileStateChanged = 2,
    /// <summary>Client -> server. Request to toggle a tile's mask, optionally with a PIN.</summary>
    ToggleRequest = 100,
    /// <summary>Server -> client. Outcome of a previous <see cref="ToggleRequest"/>.</summary>
    ToggleResponse = 101,
    /// <summary>Client -> server. One-shot JPEG snapshot of the tile's current frame.</summary>
    SnapshotRequest = 200,
    /// <summary>Server -> client. JPEG bytes (base64) or a "masked"/"empty" reason.</summary>
    SnapshotResponse = 201,
}

public enum ToggleResult
{
    Ok,
    PinRequired,
    PinWrong,
    LockedOut,
    InvalidSlot,
    /// <summary>
    /// AD-mode unmask: the request did not include validated Windows credentials.
    /// Browser must show the username/password modal and re-submit.
    /// </summary>
    CredentialsRequired,
    /// <summary>
    /// AD-mode unmask: the supplied credentials failed Windows authentication.
    /// Returned by the WebHost itself; Display never sees an unauthenticated AD
    /// request because the WebHost validates first.
    /// </summary>
    CredentialsWrong,
}

/// <summary>
/// Wire envelope: { "Type": ..., "Payload": <object> }. Payload is a raw JSON
/// element so receivers can deserialize it into the type the message-id implies
/// without needing polymorphic JSON support.
/// </summary>
public sealed class IpcEnvelope
{
    public IpcMessageType Type { get; set; }
    public JsonElement Payload { get; set; }
}

/// <summary>One tile slot; mirrors what the desktop UI displays.</summary>
public sealed record TileStateDto(
    int Slot,
    int CameraIndex,
    string Label,
    string Status,
    bool IsMasked,
    bool IsTimerWarning,
    string CountdownText);

/// <summary>Initial full snapshot.</summary>
public sealed record HelloDto(
    int Rows,
    int Columns,
    List<TileStateDto> Tiles);

public sealed record ToggleRequestDto(
    string RequestId,
    int Slot,
    string? Pin,
    string? Source = null,
    /// <summary>
    /// Set to true by the WebHost when it has already validated Windows
    /// credentials in AD mode. Display trusts this flag because the named pipe
    /// is a same-machine boundary; browser clients cannot set it directly.
    /// </summary>
    bool PreAuthenticated = false);

public sealed record ToggleResponseDto(
    string RequestId,
    ToggleResult Result,
    int? LockoutSecondsRemaining);

public sealed record SnapshotRequestDto(string RequestId, int Slot);

public enum SnapshotStatus
{
    Ok,
    Empty,    // tile has no camera bound
    NoFrame,  // camera bound but no frame yet
    Masked,   // privacy mask is active — refused by design
}

public sealed record SnapshotResponseDto(
    string RequestId,
    int Slot,
    SnapshotStatus Status,
    /// <summary>Base64-encoded JPEG payload. Null unless Status == Ok.</summary>
    string? JpegBase64);

/// <summary>Shared <see cref="JsonSerializerOptions"/> for both ends of the pipe.</summary>
public static class IpcJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}
