using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace InteractiveMask.Display;

/// <summary>
/// Persists the privacy state (mask flags, session PIN, mask timestamps) to a
/// JSON file so a crash or reboot does not leave a patient unprotected. Writes
/// are debounced ~1 s to absorb bursts of changes during a click flow.
///
/// Storage location: <c>%LOCALAPPDATA%\InteractiveMask\state.json</c>. For
/// production hardening (DPAPI-encryption, machine-wide ProgramData) see fase 9.
/// </summary>
public sealed class StateStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly DispatcherTimer _debounce;
    private PersistedState? _pendingState;

    public StateStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InteractiveMask");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "state.json");

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _debounce.Tick += (_, _) => FlushPending();
    }

    /// <summary>Read persisted state on startup; returns null if absent or corrupt.</summary>
    public PersistedState? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var json = File.ReadAllText(_path);
            var stored = JsonSerializer.Deserialize<StoredState>(json, JsonOpts);
            if (stored is null) return null;
            return new PersistedState(
                ActivePin: DecryptPin(stored.ActivePinEncrypted),
                MaskedCount: stored.MaskedCount,
                Tiles: stored.Tiles ?? new List<PersistedTile>());
        }
        catch
        {
            // Corrupt or unreadable state should not block startup.
            return null;
        }
    }

    /// <summary>Queue a save; the file is written ~1 s after the last call.</summary>
    public void RequestSave(PersistedState state)
    {
        _pendingState = state;
        _debounce.Stop();
        _debounce.Start();
    }

    private static string? EncryptPin(string? pin)
    {
        if (string.IsNullOrEmpty(pin)) return null;
        try
        {
            if (!OperatingSystem.IsWindows()) return null;
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(pin), null, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(encrypted);
        }
        catch { return null; }
    }

    private static string? DecryptPin(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            if (!OperatingSystem.IsWindows()) return null;
            var data = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine));
        }
        catch { return null; }
    }

    /// <summary>Flush any pending save synchronously. Call before app shutdown.</summary>
    public void Flush()
    {
        _debounce.Stop();
        FlushPending();
    }

    private void FlushPending()
    {
        _debounce.Stop();
        var state = _pendingState;
        _pendingState = null;
        if (state is null) return;

        try
        {
            // The session PIN is the only sensitive field; encrypt it and store the
            // rest as readable JSON so the file remains debuggable.
            var stored = new StoredState
            {
                ActivePinEncrypted = EncryptPin(state.ActivePin),
                MaskedCount = state.MaskedCount,
                Tiles = state.Tiles,
            };
            var json = JsonSerializer.Serialize(stored, JsonOpts);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // A failed write is not fatal; we'll try again on the next save request.
        }
    }

    private sealed class StoredState
    {
        public string? ActivePinEncrypted { get; set; }
        public int MaskedCount { get; set; }
        public List<PersistedTile>? Tiles { get; set; }
    }
}

public sealed record PersistedState(
    string? ActivePin,
    int MaskedCount,
    List<PersistedTile> Tiles);

public sealed record PersistedTile(
    int Slot,
    int CameraIndex,
    bool IsMasked,
    long? MaskedAtTicksUtc,
    int AutoUnmaskMinutes);
