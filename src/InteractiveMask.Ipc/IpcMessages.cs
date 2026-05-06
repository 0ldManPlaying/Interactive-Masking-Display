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
}

public enum ToggleResult
{
    Ok,
    PinRequired,
    PinWrong,
    LockedOut,
    InvalidSlot,
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

public sealed record ToggleRequestDto(string RequestId, int Slot, string? Pin, string? Source = null);

public sealed record ToggleResponseDto(
    string RequestId,
    ToggleResult Result,
    int? LockoutSecondsRemaining);

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
