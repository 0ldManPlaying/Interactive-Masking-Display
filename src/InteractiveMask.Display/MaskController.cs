using InteractiveMask.Ipc;
using System.Windows;

namespace InteractiveMask.Display;

/// <summary>
/// Coordinates the user-facing toggle of a tile's privacy mask, including all
/// PIN-related friction and the no-PIN auto-expire path used by the timer.
/// Every state-changing action is mirrored to <see cref="AuditLog"/>.
/// </summary>
public sealed class MaskController
{
    private const string SourceLocal = "local";
    private const string SourceAuto  = "auto";

    private readonly Window _owner;
    private readonly BlurPinService _pinService;
    private readonly AuditLog _audit;
    private readonly Func<int> _autoUnmaskMinutesProvider;
    private readonly Func<bool> _requireSessionPinProvider;
    private readonly Func<AuthSettings> _authSettingsProvider;
    private readonly Action _onStateChanged;

    public MaskController(
        Window owner,
        BlurPinService pinService,
        AuditLog audit,
        Func<int> autoUnmaskMinutesProvider,
        Func<bool> requireSessionPinProvider,
        Func<AuthSettings> authSettingsProvider,
        Action onStateChanged)
    {
        _owner = owner;
        _pinService = pinService;
        _audit = audit;
        _autoUnmaskMinutesProvider = autoUnmaskMinutesProvider;
        _requireSessionPinProvider = requireSessionPinProvider;
        _authSettingsProvider = authSettingsProvider;
        _onStateChanged = onStateChanged;
    }

    /// <summary>
    /// True when the current overall state is the result of a mass-mask
    /// gesture. While set, MainWindow's per-tile auto-unmask tick must not
    /// fire so individual countdown timers do not leak open during the
    /// "caregiver away" scenario.
    /// </summary>
    public bool IsMassHoldActive { get; private set; }

    /// <summary>
    /// User clicked a tile. Apply the mask immediately if it was off, or run the
    /// PIN-verify flow if it was on.
    /// </summary>
    public void HandleTileClick(TileViewModel tile)
    {
        if (!tile.HasCamera) return;

        if (!tile.IsMasked)
        {
            ApplyMaskLocal(tile);
        }
        else
        {
            TryRemoveMaskLocal(tile);
        }
    }

    /// <summary>
    /// Auto-unmask path: triggered by the timer when a mask reaches its expiry.
    /// Bypasses the PIN check by design (the timer is not a user).
    /// </summary>
    public void AutoExpireMask(TileViewModel tile)
    {
        if (!tile.IsMasked) return;
        tile.SetMasked(false);
        _pinService.OnMaskRemovedExternal();
        _audit.Write(AuditEventType.MaskAutoReset, slot: tile.SlotIndex, label: tile.Label, source: SourceAuto);
        _onStateChanged();
    }

    /// <summary>
    /// Remote-control entry point used by the WebHost over IPC. Same security
    /// rules as the desktop click flow but without modal dialogs. Respects
    /// AD-mode: the WebHost validates Windows credentials itself and forwards
    /// the request with <see cref="ToggleRequestDto.PreAuthenticated"/>=true,
    /// which acts here as the desktop equivalent of "credential prompt OK".
    /// </summary>
    public ToggleResult HandleRemoteToggle(TileViewModel tile, string? pin, string? source, bool preAuthenticated = false)
    {
        var src = string.IsNullOrEmpty(source) ? "remote" : source;
        var auth = _authSettingsProvider();
        bool pinEnabled = _requireSessionPinProvider();

        if (!tile.HasCamera) return ToggleResult.InvalidSlot;

        // AD mode wins over PIN mode (matches the desktop flow in TryRemoveMaskLocal).
        if (auth.UseActiveDirectory)
        {
            return HandleAdRemoteToggle(tile, src, preAuthenticated);
        }

        if (!tile.IsMasked)
        {
            // Apply mask. PIN is only required when the policy is on and no session
            // PIN exists yet (this becomes the new session PIN).
            if (!pinEnabled || _pinService.HasActivePin)
            {
                tile.SetMasked(true, _autoUnmaskMinutesProvider());
                _pinService.OnMaskApplied();
                _audit.Write(AuditEventType.MaskOn, slot: tile.SlotIndex, label: tile.Label, source: src);
                _onStateChanged();
                return ToggleResult.Ok;
            }

            if (string.IsNullOrEmpty(pin)) return ToggleResult.PinRequired;

            tile.SetMasked(true, _autoUnmaskMinutesProvider());
            _pinService.OnMaskApplied();
            _pinService.SetSessionPin(pin);
            _audit.Write(AuditEventType.PinSet, source: src);
            _audit.Write(AuditEventType.MaskOn, slot: tile.SlotIndex, label: tile.Label, source: src);
            _onStateChanged();
            return ToggleResult.Ok;
        }

        // Unmask path. With PIN policy off, anyone can remove a mask.
        if (!pinEnabled)
        {
            tile.SetMasked(false);
            _pinService.OnMaskRemovedExternal();
            _audit.Write(AuditEventType.MaskOff, slot: tile.SlotIndex, label: tile.Label, source: src);
            _onStateChanged();
            return ToggleResult.Ok;
        }

        if (_pinService.IsLockedOut) return ToggleResult.LockedOut;
        if (string.IsNullOrEmpty(pin)) return ToggleResult.PinRequired;

        if (_pinService.VerifyAndConsumeForUnmask(pin))
        {
            tile.SetMasked(false);
            _audit.Write(AuditEventType.MaskOff, slot: tile.SlotIndex, label: tile.Label, source: src);
            _onStateChanged();
            return ToggleResult.Ok;
        }

        _audit.Write(AuditEventType.PinFail, slot: tile.SlotIndex, label: tile.Label, source: src);
        if (_pinService.IsLockedOut)
        {
            _audit.Write(AuditEventType.PinLockout, source: src);
            return ToggleResult.LockedOut;
        }
        return ToggleResult.PinWrong;
    }

    private ToggleResult HandleAdRemoteToggle(TileViewModel tile, string src, bool preAuthenticated)
    {
        // Apply: free, privacy-first (mirrors the desktop ApplyMaskLocal flow which
        // never prompts for credentials when *enabling* a mask).
        if (!tile.IsMasked)
        {
            tile.SetMasked(true, _autoUnmaskMinutesProvider());
            _pinService.OnMaskApplied();
            _audit.Write(AuditEventType.MaskOn, slot: tile.SlotIndex, label: tile.Label, source: src);
            _onStateChanged();
            return ToggleResult.Ok;
        }

        // Unmask: WebHost must have validated Windows credentials and set the
        // pre-authenticated flag. Display does not re-validate because LogonUser
        // is already done; we trust the same-machine pipe boundary.
        if (!preAuthenticated) return ToggleResult.CredentialsRequired;

        tile.SetMasked(false);
        _pinService.OnMaskRemovedExternal();
        _audit.Write(AuditEventType.MaskOff, slot: tile.SlotIndex, label: tile.Label, source: src);
        _onStateChanged();
        return ToggleResult.Ok;
    }

    private void ApplyMaskLocal(TileViewModel tile)
    {
        // Privacy first: blur is applied instantly so there's no window of visibility
        // while the caregiver thinks about a PIN or login.
        tile.SetMasked(true, _autoUnmaskMinutesProvider());
        _pinService.OnMaskApplied();
        _audit.Write(AuditEventType.MaskOn, slot: tile.SlotIndex, label: tile.Label, source: SourceLocal);
        _onStateChanged();

        // Only the PIN-mode flow needs to capture a session secret on first mask.
        // AD-mode authenticates per unmask, so there is nothing to do here.
        var auth = _authSettingsProvider();
        if (!auth.UseActiveDirectory && _requireSessionPinProvider() && !_pinService.HasActivePin)
        {
            var pin = PinDialog.PromptForNewPin(_owner);
            if (!string.IsNullOrEmpty(pin))
            {
                _pinService.SetSessionPin(pin);
                _audit.Write(AuditEventType.PinSet, source: SourceLocal);
                _onStateChanged();
            }
        }
    }

    private void TryRemoveMaskLocal(TileViewModel tile)
    {
        var auth = _authSettingsProvider();

        // Authentication completely disabled.
        if (!_requireSessionPinProvider() && !auth.UseActiveDirectory)
        {
            tile.SetMasked(false);
            _pinService.OnMaskRemovedExternal();
            _audit.Write(AuditEventType.MaskOff, slot: tile.SlotIndex, label: tile.Label, source: SourceLocal);
            _onStateChanged();
            return;
        }

        if (auth.UseActiveDirectory)
        {
            TryRemoveMaskWithAd(tile, auth.Domain);
        }
        else
        {
            TryRemoveMaskWithPin(tile);
        }
    }

    private void TryRemoveMaskWithAd(TileViewModel tile, string defaultDomain)
    {
        var s = Strings.Instance.Current;
        var (ok, username) = CredentialDialog.Prompt(
            owner: _owner,
            title: s.SetupAdRemoveTitle,
            subtitle: s.SetupAdLoginSubtitle,
            verifier: (user, password) =>
            {
                var domain = ExtractDomainOr(defaultDomain);
                var result = WindowsAuth.Authenticate(user, password, domain);
                if (!result.Success)
                {
                    _audit.Write(AuditEventType.PinFail,
                        slot: tile.SlotIndex, label: tile.Label,
                        source: $"user:{user}", detail: result.Error);
                }
                return result;
            });

        if (!ok) return;

        tile.SetMasked(false);
        _pinService.OnMaskRemovedExternal();
        _audit.Write(AuditEventType.MaskOff,
            slot: tile.SlotIndex, label: tile.Label,
            source: $"user:{username}");
        _onStateChanged();
    }

    private static string ExtractDomainOr(string defaultDomain) => defaultDomain;

    private void TryRemoveMaskWithPin(TileViewModel tile)
    {
        bool ok = PinDialog.PromptToVerify(
            _owner,
            verifier: pin =>
            {
                bool valid = _pinService.VerifyAndConsumeForUnmask(pin);
                if (!valid)
                {
                    _audit.Write(AuditEventType.PinFail, slot: tile.SlotIndex, label: tile.Label, source: SourceLocal);
                    if (_pinService.IsLockedOut)
                    {
                        _audit.Write(AuditEventType.PinLockout, source: SourceLocal);
                    }
                }
                return valid;
            },
            lockoutCheck: () => _pinService.LockoutRemaining);

        if (ok)
        {
            tile.SetMasked(false);
            _audit.Write(AuditEventType.MaskOff, slot: tile.SlotIndex, label: tile.Label, source: SourceLocal);
            _onStateChanged();
        }
    }

    // ---- Mass mask / unmask (v1.3.0 item 1) -----------------------------------
    //
    // Long-press gesture entry-point. Looks at the current global state and
    // decides whether to apply a mass-mask (privacy-first, no auth) or to
    // request a mass-unmask (auth via the existing policy, optional confirm).
    //
    // State rules:
    //   - All tiles already masked  -> mass-unmask path.
    //   - Anything else (mixed or fully visible) -> mass-mask path. This
    //     intentionally never reaches the "blocked because of mixed state"
    //     case the roadmap mentions: in a mixed state we top up to fully
    //     masked, which is always safe.

    public void HandleLongPress(IEnumerable<TileViewModel> allTiles, bool showUnmaskConfirm)
    {
        var withCamera = allTiles.Where(t => t.HasCamera).ToList();
        if (withCamera.Count == 0) return;

        bool allMasked = withCamera.All(t => t.IsMasked);
        if (allMasked)
        {
            TryRemoveMassMask(withCamera, showUnmaskConfirm);
        }
        else
        {
            ApplyMassMask(withCamera);
        }
    }

    private void ApplyMassMask(List<TileViewModel> tiles)
    {
        // Override every tile's auto-unmask timer with 0 (no auto-expire) so
        // the mass-hold doesn't leak open while the caregiver is away. Tiles
        // that were already masked also get their timer cleared, intentionally:
        // the new global intent ("everyone hold, I'm leaving the post") is
        // stronger than any per-tile timer that was running.
        int affected = 0;
        foreach (var tile in tiles)
        {
            if (!tile.IsMasked) affected++;
            tile.SetMasked(true, autoUnmaskMinutes: 0);
        }
        _pinService.OnMaskApplied();
        IsMassHoldActive = true;
        _audit.Write(AuditEventType.MassMaskOn, source: SourceLocal,
                     detail: $"tiles={tiles.Count};newlyMasked={affected}");
        _onStateChanged();
    }

    private void TryRemoveMassMask(List<TileViewModel> tiles, bool showConfirm)
    {
        // State guard: caller already checked that all tiles are masked, but
        // re-check defensively. If something has changed in the meantime
        // (race with auto-unmask, or with a remote toggle) drop silently.
        if (tiles.Any(t => !t.IsMasked)) return;

        var s = Strings.Instance.Current;

        if (showConfirm)
        {
            var result = MessageBox.Show(
                _owner,
                s.MassUnmaskConfirmBody,
                s.MassUnmaskConfirmTitle,
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);
            if (result != MessageBoxResult.OK) return;
        }

        var auth = _authSettingsProvider();
        bool pinEnabled = _requireSessionPinProvider();

        // Auth mirrors the per-tile flow exactly. AD wins over PIN; PIN-off
        // means anyone can release.
        if (auth.UseActiveDirectory)
        {
            var (ok, username) = CredentialDialog.Prompt(
                owner: _owner,
                title: s.MassUnmaskAuthTitle,
                subtitle: s.SetupAdLoginSubtitle,
                verifier: (user, password) =>
                {
                    var domain = ExtractDomainOr(auth.Domain);
                    return WindowsAuth.Authenticate(user, password, domain);
                });
            if (!ok) return;
            ApplyMassUnmaskInternal(tiles, $"user:{username}");
            return;
        }

        if (pinEnabled)
        {
            if (_pinService.IsLockedOut) return;
            bool pinOk = PinDialog.PromptToVerify(
                _owner,
                verifier: pin =>
                {
                    bool valid = _pinService.VerifyAndConsumeForUnmask(pin);
                    if (!valid)
                    {
                        _audit.Write(AuditEventType.PinFail, source: SourceLocal,
                                     detail: "mass-unmask");
                        if (_pinService.IsLockedOut)
                        {
                            _audit.Write(AuditEventType.PinLockout, source: SourceLocal,
                                         detail: "mass-unmask");
                        }
                    }
                    return valid;
                },
                lockoutCheck: () => _pinService.LockoutRemaining);
            if (!pinOk) return;
            ApplyMassUnmaskInternal(tiles, SourceLocal);
            return;
        }

        // Auth fully disabled: release immediately.
        ApplyMassUnmaskInternal(tiles, SourceLocal);
    }

    private void ApplyMassUnmaskInternal(List<TileViewModel> tiles, string source)
    {
        int affected = 0;
        foreach (var tile in tiles)
        {
            if (tile.IsMasked) affected++;
            tile.SetMasked(false);
        }
        _pinService.OnMaskRemovedExternal();
        IsMassHoldActive = false;
        _audit.Write(AuditEventType.MassMaskOff, source: source,
                     detail: $"tiles={tiles.Count};unmasked={affected}");
        _onStateChanged();
    }
}
