using InteractiveMask.Ipc;
using System.Windows;
using System.Windows.Threading;

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
    private readonly Func<PrivacyMode> _privacyModeProvider;
    private readonly Func<bool> _privacyDefaultRequireAuthProvider;
    private readonly Action _onStateChanged;

    public MaskController(
        Window owner,
        BlurPinService pinService,
        AuditLog audit,
        Func<int> autoUnmaskMinutesProvider,
        Func<bool> requireSessionPinProvider,
        Func<AuthSettings> authSettingsProvider,
        Func<PrivacyMode> privacyModeProvider,
        Func<bool> privacyDefaultRequireAuthProvider,
        Action onStateChanged)
    {
        _owner = owner;
        _pinService = pinService;
        _audit = audit;
        _autoUnmaskMinutesProvider = autoUnmaskMinutesProvider;
        _requireSessionPinProvider = requireSessionPinProvider;
        _authSettingsProvider = authSettingsProvider;
        _privacyModeProvider = privacyModeProvider;
        _privacyDefaultRequireAuthProvider = privacyDefaultRequireAuthProvider;
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
    /// User clicked a tile. Routes to the correct branch based on the
    /// current <see cref="PrivacyMode"/>. In both modes, applying privacy
    /// (a transition into <c>IsMasked=true</c>) is always free; removing
    /// privacy is auth-aware with mode-specific rules.
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
    /// Auto-unmask path (oversight-default mode): triggered by the timer
    /// when a mask reaches its expiry. Bypasses the PIN check by design
    /// (the timer is not a user).
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
    /// Auto-re-mask path (privacy-default mode): triggered by the timer
    /// when a revealed tile's reveal-window expires and the tile must
    /// snap back to the masked default state. No auth, no audit-as-user
    /// (mirrors AutoExpireMask).
    /// </summary>
    public void AutoReMaskTile(TileViewModel tile)
    {
        if (tile.IsMasked) return;
        tile.SetMasked(true, autoMinutes: 0);
        _audit.Write(AuditEventType.MaskAutoReset, slot: tile.SlotIndex, label: tile.Label, source: SourceAuto);
        _onStateChanged();
    }

    /// <summary>
    /// Called once after grid setup when the configured mode is
    /// <see cref="PrivacyMode.PrivacyDefault"/>. Forces every camera-bound
    /// tile into the masked default state without writing audit events
    /// (this is the default state, not a user action).
    /// <para>
    /// Each primed tile is also reported to <see cref="BlurPinService"/>
    /// via <see cref="BlurPinService.OnMaskApplied"/> so the masked-tile
    /// counter matches the visible state. Without this, the counter starts
    /// at 0 while N tiles are visibly masked; the first unmask later would
    /// clamp the counter back to 0 and prematurely clear the session PIN
    /// while other tiles are still masked — a privacy regression.
    /// </para>
    /// </summary>
    public void PrimePrivacyDefaultState(IEnumerable<TileViewModel> allTiles)
    {
        foreach (var tile in allTiles)
        {
            if (!tile.HasCamera) continue;
            tile.SetMasked(true, autoMinutes: 0);
            _pinService.OnMaskApplied();
        }
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
        //
        // Auto-timer wiring is mode-dependent. In oversight-default mode the mask
        // is the *non-default* state, so we arm an auto-unmask so it doesn't get
        // forgotten. In privacy-default mode the mask IS the default state, so
        // applying a mask is "return to default": no timer, the tile stays
        // masked until the user explicitly reveals again.
        var mode = _privacyModeProvider();
        int autoMinutes = mode == PrivacyMode.OversightDefault ? _autoUnmaskMinutesProvider() : 0;
        tile.SetMasked(true, autoMinutes);
        _pinService.OnMaskApplied();
        _audit.Write(AuditEventType.MaskOn, slot: tile.SlotIndex, label: tile.Label, source: SourceLocal);

        // Full-tile privacy mask wins: any active AI reveal on this tile is
        // moot once the entire tile is blurred. Cancel so the badge clears
        // and the audit trail records the supersession.
        if (tile.IsAiRevealed) CancelAiReveal(tile, reason: "full-tile-mask");

        _onStateChanged();

        // Only the PIN-mode flow needs to capture a session secret on first mask,
        // and only in oversight-default (where the mask blocks oversight). In
        // privacy-default the mask is the safe default and does not need a PIN
        // to be captured at this moment; PIN capture happens on first reveal
        // attempt instead, via the auth path below.
        if (mode != PrivacyMode.OversightDefault) return;

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
        var mode = _privacyModeProvider();

        // Mode-specific decision on whether this transition needs auth at all:
        //
        //  - OversightDefault: removing a mask is "lifting privacy from the
        //    default-visible grid", so it follows the configured policy
        //    (PIN/AD if either is enabled, free if both are off).
        //
        //  - PrivacyDefault: removing the default-blur is a deliberate reveal
        //    for verification, gated by PrivacyDefaultRequireAuthOnReveal.
        //    When that toggle is off, reveals are free because the auto-rollback
        //    timer brings privacy back. When on, we run the same PIN/AD path.
        bool needsAuth = mode == PrivacyMode.PrivacyDefault
            ? _privacyDefaultRequireAuthProvider() && (_requireSessionPinProvider() || auth.UseActiveDirectory)
            : (_requireSessionPinProvider() || auth.UseActiveDirectory);

        if (!needsAuth)
        {
            tile.SetMasked(false, EffectiveRevealAutoMinutes(mode));
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

    /// <summary>
    /// Minutes to arm the post-reveal auto-rollback. Non-zero only in
    /// privacy-default mode; oversight-default leaves the tile open until
    /// the user (or auto-unmask on the masked side) acts.
    /// </summary>
    private int EffectiveRevealAutoMinutes(PrivacyMode mode)
        => mode == PrivacyMode.PrivacyDefault ? _autoUnmaskMinutesProvider() : 0;

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

        tile.SetMasked(false, EffectiveRevealAutoMinutes(_privacyModeProvider()));
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
            tile.SetMasked(false, EffectiveRevealAutoMinutes(_privacyModeProvider()));
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
            bool wasUnmasked = !tile.IsMasked;
            tile.SetMasked(true, autoMinutes: 0);
            if (wasUnmasked)
            {
                affected++;
                // Bump the PIN-service counter once per *newly* masked tile
                // (mirroring the per-tile ApplyMaskLocal flow). Without this
                // the counter only increments by 1 for the whole batch and a
                // single later unmask drains it to 0, which clears _activePin
                // and turns subsequent unmasks into no-auth ops — anyone
                // walking by could lift every privacy mask.
                _pinService.OnMaskApplied();
            }
        }

        // Capture a session PIN if the policy demands one and none is active
        // yet, exactly like the first individual ApplyMaskLocal would. Skipped
        // in AD-mode (auth happens per unmask there, no session secret).
        var auth = _authSettingsProvider();
        if (!auth.UseActiveDirectory && _requireSessionPinProvider() && !_pinService.HasActivePin && affected > 0)
        {
            var pin = PinDialog.PromptForNewPin(_owner);
            if (!string.IsNullOrEmpty(pin))
            {
                _pinService.SetSessionPin(pin);
                _audit.Write(AuditEventType.PinSet, source: SourceLocal);
            }
        }

        IsMassHoldActive = true;
        _audit.Write(AuditEventType.MassMaskOn, source: SourceLocal,
                     detail: $"tiles={tiles.Count};newlyMasked={affected}");

        // Mass-mask wins: any active per-tile AI reveal is meaningless once
        // every tile is fully blurred at the privacy-mask level. Force-clear.
        CancelAllAiReveals(reason: "mass-mask");

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

        // Mass-actions cancel any in-flight AI reveals: the new global intent
        // is "everyone hold / everyone release", individual per-tile reveal
        // windows are no longer relevant after a fleet-wide gesture.
        CancelAllAiReveals(reason: "mass-mask");

        _onStateChanged();
    }

    // ---- AI-reveal flow (v2.0.x F2) -------------------------------------------
    //
    // Reviewer clicks the small AI icon on a tile with active AI detections.
    // After auth (same policy as v1.x unmask, but non-consuming on the PIN
    // counter), a duration picker appears. The chosen window is recorded on
    // the TileViewModel; a single 1 Hz DispatcherTimer drives both the
    // countdown badge and the auto-restore.
    //
    // Per-detection reveal ("blur this plate but not the other") is
    // intentionally not supported - architecture doc §12.

    private const string SourceManualRemask = "manual-remask";
    private const string SourceTimerExpired = "timer-expired";

    /// <summary>Tracks tiles with an active reveal so the timer only ticks
    /// when there's something to do.</summary>
    private readonly HashSet<TileViewModel> _activeReveals = new();
    private DispatcherTimer? _revealTicker;

    /// <summary>
    /// Entry point invoked when the reviewer clicks the AI icon on a tile.
    /// Runs the auth flow per the configured policy, then prompts for a
    /// duration, then applies the reveal. No-ops silently if the tile has
    /// AI disabled / the operator cancels / auth fails.
    /// </summary>
    public void RequestAiReveal(TileViewModel tile)
    {
        if (!tile.HasCamera || !tile.AiEnabled) return;
        if (tile.IsAiRevealed) return;   // already revealed; clicking again is a no-op

        var auth = _authSettingsProvider();
        bool pinEnabled = _requireSessionPinProvider();
        string source;

        // Auth gate. Mirrors TryRemoveMaskLocal: AD wins over PIN; both off
        // means a free reveal (matches the v1.x rule that with auth disabled
        // anyone can lift privacy).
        if (auth.UseActiveDirectory)
        {
            var s = Strings.Instance.Current;
            var (ok, username) = CredentialDialog.Prompt(
                owner: _owner,
                title: s.AiRevealDialogTitle,
                subtitle: s.SetupAdLoginSubtitle,
                verifier: (user, password) =>
                {
                    var domain = ExtractDomainOr(auth.Domain);
                    var result = WindowsAuth.Authenticate(user, password, domain);
                    if (!result.Success)
                    {
                        _audit.Write(AuditEventType.PinFail,
                            slot: tile.SlotIndex, label: tile.Label,
                            source: $"user:{user}", detail: "ai-reveal: " + result.Error);
                    }
                    return result;
                });
            if (!ok) return;
            source = $"user:{username}";
        }
        else if (pinEnabled)
        {
            if (_pinService.IsLockedOut) return;
            bool pinOk = PinDialog.PromptToVerify(
                _owner,
                verifier: pin =>
                {
                    // Non-consuming: do not touch _maskedCount / _activePin.
                    // A successful AI-reveal must not clear the session PIN.
                    bool valid = _pinService.VerifyForReadOnlyAction(pin);
                    if (!valid)
                    {
                        _audit.Write(AuditEventType.PinFail,
                            slot: tile.SlotIndex, label: tile.Label,
                            source: SourceLocal, detail: "ai-reveal");
                        if (_pinService.IsLockedOut)
                        {
                            _audit.Write(AuditEventType.PinLockout,
                                source: SourceLocal, detail: "ai-reveal");
                        }
                    }
                    return valid;
                },
                lockoutCheck: () => _pinService.LockoutRemaining);
            if (!pinOk) return;
            source = SourceLocal;
        }
        else
        {
            source = SourceLocal;
        }

        // Auth ok → pick a duration. Cancel here drops out silently; no audit
        // event is written because nothing changed.
        var duration = AiRevealDurationDialog.Prompt(_owner, tile.Label ?? "");
        if (duration is null) return;

        ApplyAiReveal(tile, duration.Value, source);
    }

    private void ApplyAiReveal(TileViewModel tile, TimeSpan duration, string source)
    {
        DateTime untilUtc;
        string detailDuration;
        if (duration == AiRevealDurationDialog.UntilRemask)
        {
            untilUtc = DateTime.MaxValue;
            detailDuration = "indefinite";
        }
        else
        {
            untilUtc = DateTime.UtcNow + duration;
            detailDuration = $"{(int)duration.TotalSeconds}s";
        }

        tile.AiRevealedUntilUtc = untilUtc;
        _activeReveals.Add(tile);
        EnsureRevealTickerRunning();

        _audit.Write(AuditEventType.AiRevealRequested,
            slot: tile.SlotIndex, label: tile.Label,
            source: source, detail: detailDuration);
        _onStateChanged();
    }

    /// <summary>
    /// Cut short the active reveal on one tile. Called when the reviewer
    /// presses the on-tile "remask" badge, when the auto-restore timer
    /// fires, or when an external state change (manual full-tile mask,
    /// mass-mask, camera rebind) overrides the reveal.
    /// </summary>
    public void CancelAiReveal(TileViewModel tile, string reason)
    {
        if (!tile.IsAiRevealed) return;
        tile.AiRevealedUntilUtc = null;
        _activeReveals.Remove(tile);
        if (_activeReveals.Count == 0) StopRevealTicker();

        _audit.Write(AuditEventType.AiRevealExpired,
            slot: tile.SlotIndex, label: tile.Label,
            source: SourceLocal, detail: reason);
        _onStateChanged();
    }

    /// <summary>
    /// Force-cancel every active reveal. Called on mass-mask, mass-unmask, and
    /// from Setup → Apply when camera bindings change.
    /// </summary>
    public void CancelAllAiReveals(string reason)
    {
        if (_activeReveals.Count == 0) return;
        // Snapshot to a list so CancelAiReveal can mutate _activeReveals safely.
        foreach (var tile in _activeReveals.ToList())
        {
            tile.AiRevealedUntilUtc = null;
            _audit.Write(AuditEventType.AiRevealExpired,
                slot: tile.SlotIndex, label: tile.Label,
                source: SourceLocal, detail: reason);
        }
        _activeReveals.Clear();
        StopRevealTicker();
        _onStateChanged();
    }

    private void EnsureRevealTickerRunning()
    {
        if (_revealTicker is not null) return;
        _revealTicker = new DispatcherTimer(DispatcherPriority.Background, _owner.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _revealTicker.Tick += OnRevealTick;
        _revealTicker.Start();
    }

    private void StopRevealTicker()
    {
        if (_revealTicker is null) return;
        _revealTicker.Stop();
        _revealTicker.Tick -= OnRevealTick;
        _revealTicker = null;
    }

    private void OnRevealTick(object? sender, EventArgs e)
    {
        // Snapshot first - CancelAiReveal mutates _activeReveals.
        DateTime nowUtc = DateTime.UtcNow;
        var expired = new List<TileViewModel>();

        foreach (var tile in _activeReveals)
        {
            // Refresh the countdown text on all live reveals so the badge
            // ticks even for the "until I remask" tiles (no-op there, since
            // AiRevealRemaining returns TimeSpan.MaxValue and the XAML
            // converter prints the indefinite-label).
            tile.NotifyAiRevealCountdownTick();

            if (tile.AiRevealedUntilUtc is { } until
                && until != DateTime.MaxValue
                && until <= nowUtc)
            {
                expired.Add(tile);
            }
        }

        foreach (var tile in expired)
        {
            CancelAiReveal(tile, SourceTimerExpired);
        }
    }
}
