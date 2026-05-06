namespace InteractiveMask.Display;

/// <summary>
/// Holds the session-scoped privacy-mask PIN. Implements the "option 2" scope
/// agreed with the user: one active PIN guards all currently-masked tiles; once
/// every tile is unmasked the PIN is forgotten and the next first-mask-on starts
/// a fresh session with a new PIN.
///
/// Thread-affinity: all calls expected on the WPF UI thread (the MaskController
/// drives this from click handlers and dialog callbacks).
/// </summary>
public sealed class BlurPinService
{
    private const int MaxFailedAttempts = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

    private string? _activePin;
    private int _maskedCount;
    private int _failedAttempts;
    private DateTime? _lockoutUntil;

    public bool HasActivePin => _activePin is not null;

    public bool IsLockedOut => _lockoutUntil is { } until && DateTime.UtcNow < until;

    public TimeSpan LockoutRemaining => _lockoutUntil is { } until && DateTime.UtcNow < until
        ? until - DateTime.UtcNow
        : TimeSpan.Zero;

    /// <summary>
    /// Initialise the session PIN on the very first mask-on of a session. Subsequent
    /// calls while a PIN is already active are silently ignored.
    /// </summary>
    public void SetSessionPin(string pin)
    {
        if (_activePin is not null) return;
        if (string.IsNullOrEmpty(pin)) throw new ArgumentException("pin must not be empty", nameof(pin));
        _activePin = pin;
        _failedAttempts = 0;
    }

    /// <summary>Bump the masked-tile counter. Call when a tile transitions to masked.</summary>
    public void OnMaskApplied() => _maskedCount++;

    /// <summary>
    /// Validate a PIN entered by the user when unmasking. Returns true if it matches
    /// the session PIN (which clears failed attempts). On a wrong PIN, increments the
    /// failed-attempts counter and triggers a lockout after <see cref="MaxFailedAttempts"/>.
    /// </summary>
    public bool VerifyAndConsumeForUnmask(string pin)
    {
        if (IsLockedOut) return false;
        if (_activePin is null)
        {
            DecrementMaskCount();
            return true; // defensive: no PIN ever set, allow unmask
        }
        if (pin == _activePin)
        {
            _failedAttempts = 0;
            DecrementMaskCount();
            return true;
        }

        _failedAttempts++;
        if (_failedAttempts >= MaxFailedAttempts)
        {
            _lockoutUntil = DateTime.UtcNow + LockoutDuration;
            _failedAttempts = 0;
        }
        return false;
    }

    /// <summary>
    /// Decrement the masked-tile counter without verifying a PIN. Used by the
    /// auto-unmask timer path: the timer is not a user, so it shouldn't be subject
    /// to PIN entry, but the counter still has to track the real state so the
    /// session-PIN clears correctly when the last mask drops.
    /// </summary>
    public void OnMaskRemovedExternal() => DecrementMaskCount();

    /// <summary>
    /// Restore the session PIN + counters from persisted state on app startup.
    /// </summary>
    public void Restore(string? activePin, int maskedCount)
    {
        _activePin = activePin;
        _maskedCount = Math.Max(0, maskedCount);
        _failedAttempts = 0;
        _lockoutUntil = null;
    }

    public string? CurrentSessionPin => _activePin;
    public int CurrentMaskedCount => _maskedCount;

    private void DecrementMaskCount()
    {
        _maskedCount = Math.Max(0, _maskedCount - 1);
        if (_maskedCount == 0)
        {
            _activePin = null;
            _lockoutUntil = null;
        }
    }
}
