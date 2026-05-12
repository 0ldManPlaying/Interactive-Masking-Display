using InteractiveMask.Detection;
using InteractiveMask.Gdk;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace InteractiveMask.Display;

public partial class MainWindow : Window
{
    private readonly DisplayViewModel _viewModel = new();
    private readonly BlurPinService _pinService = new();
    private readonly AdminPinService _adminPin = new();
    private readonly StateStore _stateStore = new();
    private readonly AuditLog _audit = new();
    private readonly ConfigService _configService = new();
    /// <summary>Per-slot binding to a unique camera identity (NVR + camera index).
    /// Replaces the old single-NVR <c>_slotByCameraIndex</c> map.</summary>
    private readonly Dictionary<int, (int NvrId, int CameraIndex)> _bindingsBySlot = new();
    /// <summary>One <see cref="NvrSession"/> per configured NVR id. Lifetime
    /// managed alongside the rest of the app: created on load, hot-swapped on
    /// structural NVR changes, all disposed on Closing.</summary>
    private readonly Dictionary<int, NvrSession> _sessionsById = new();
    /// <summary>Per-NVR latest disconnect label. The reconnect banner aggregates
    /// across all NVRs ("Verbinding met N NVRs verloren — opnieuw...").</summary>
    private readonly Dictionary<int, string> _disconnectLabelByNvr = new();
    /// <summary>Per-NVR earliest scheduled reconnect time. The banner shows the
    /// soonest one so the countdown reflects the next attempt across the fleet.</summary>
    private readonly Dictionary<int, DateTime> _nextReconnectByNvr = new();
    private readonly DispatcherTimer _timerTick = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>
    /// v2.0 AI-masking detector. Created and initialised asynchronously after the
    /// tile grid is wired; <c>null</c> while initialising and on init-failure (which
    /// silently falls back to v1.x masking with no per-detection overlay).
    /// </summary>
    private IObjectDetector? _aiDetector;

    private readonly KioskGuard _kioskGuard = new();
    private MaskController? _maskController;
    private IDisposable? _gdkLifetime;
    private IpcStateBroadcaster? _ipcBroadcaster;
    private PrivacySettings _privacy = new();
    private AuthSettings _auth = new();
    private AppSettings? _lastAppliedSettings;
    private bool _authenticatedExit;
    private SyslogForwarder? _auditForwarder;
    private AuditForwardSettings _auditForward = new();

    public MainWindow()
    {
        BootstrapLog.Step("MainWindow.ctor.begin");
        InitializeComponent();
        BootstrapLog.Step("MainWindow.ctor.after-InitializeComponent");
        DataContext = _viewModel;
        _timerTick.Tick += OnTimerTick;
        BootstrapLog.Step("MainWindow.ctor.end");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BootstrapLog.Step("MainWindow.OnLoaded.begin");
        // First-launch admin-PIN bootstrap. Without an admin PIN configured, the
        // kiosk can be exited and reconfigured by anyone walking past — so we
        // refuse to start until one exists.
        if (!_adminPin.IsConfigured)
        {
            BootstrapLog.Step("MainWindow.OnLoaded.admin-pin-not-configured");
            var s0 = Strings.Instance.Current;
            BootstrapLog.Step("MainWindow.OnLoaded.before-PromptForNewPin");
            string? newPin = null;
            try
            {
                newPin = PinDialog.PromptForNewPin(
                    this,
                    title: s0.AdminPinFirstSetupTitle,
                    subtitle: s0.AdminPinFirstSetupSub);
            }
            catch (Exception ex)
            {
                BootstrapLog.Step("MainWindow.OnLoaded.PromptForNewPin-THREW",
                    $"{ex.GetType().Name}: {ex.Message}");
                throw;
            }
            BootstrapLog.Step("MainWindow.OnLoaded.after-PromptForNewPin",
                $"newPin={(string.IsNullOrEmpty(newPin) ? "<empty/cancelled>" : "<set>")}");
            if (string.IsNullOrEmpty(newPin))
            {
                BootstrapLog.Step("MainWindow.OnLoaded.pin-cancelled  -- closing window");
                _authenticatedExit = true;
                Close();
                return;
            }
            _adminPin.SetPin(newPin);
            _audit.Write(AuditEventType.PinSet, source: "admin", detail: "admin-pin initial setup");
        }
        else
        {
            BootstrapLog.Step("MainWindow.OnLoaded.admin-pin-already-configured");
        }

        var settings = LoadSettings();
        if (settings.Nvrs.Count == 0 || settings.Nvrs.All(n => string.IsNullOrWhiteSpace(n.Ip) || string.IsNullOrWhiteSpace(n.Password)))
        {
            _viewModel.StatusLine = Strings.Instance.Current.MainNoConfigLine;
            return;
        }

        _privacy = settings.Privacy;
        _auth = settings.Auth;
        _auditForward = settings.AuditForward;
        ApplyAuditForwarder(_auditForward);
        _audit.Write(AuditEventType.AppStarted, source: "local");
        _maskController = new MaskController(
            owner: this,
            pinService: _pinService,
            audit: _audit,
            autoUnmaskMinutesProvider: () => _privacy.AutoUnmaskMinutes,
            requireSessionPinProvider: () => _privacy.RequireSessionPin,
            authSettingsProvider: () => _auth,
            privacyModeProvider: () => _privacy.Mode,
            privacyDefaultRequireAuthProvider: () => _privacy.PrivacyDefaultRequireAuthOnReveal,
            onStateChanged: SaveState);

        int rows = Math.Max(1, settings.Grid.Rows);
        int cols = Math.Max(1, settings.Grid.Columns);
        _viewModel.InitializeGrid(rows, cols, Dispatcher);

        _gdkLifetime = GdkLifetime.Acquire("InteractiveMask.Display");

        // Spin up one NvrSession per configured NVR. Each session reconnects
        // independently; the banner aggregates their state.
        foreach (var nvr in settings.Nvrs)
        {
            if (string.IsNullOrWhiteSpace(nvr.Ip) || string.IsNullOrWhiteSpace(nvr.Password)) continue;
            CreateAndWireSession(nvr);
        }

        // Defensive registration: a corrupted config (or a manual edit) could contain
        // the same slot twice or two cameras pointing at the same (NVR, camera) pair.
        // Either would have thrown InvalidOperationException out of RegisterTile and
        // crashed the app on every startup, including after re-install (config.json
        // lives in ProgramData and survives uninstall by design).
        var seenIdentities = new HashSet<(int nvrId, int cameraIdx)>();
        var seenSlots = new HashSet<int>();
        foreach (var cam in settings.Cameras)
        {
            if (cam.Slot < 0 || cam.Slot >= _viewModel.Tiles.Count) continue;
            if (!seenSlots.Add(cam.Slot))
            {
                _audit.Write(AuditEventType.AppStarted, source: "config",
                    detail: $"duplicate slot {cam.Slot} skipped (nvr={cam.NvrId}, camera={cam.CameraIndex})");
                continue;
            }
            if (!seenIdentities.Add((cam.NvrId, cam.CameraIndex)))
            {
                _audit.Write(AuditEventType.AppStarted, source: "config",
                    detail: $"duplicate camera nvr={cam.NvrId} idx={cam.CameraIndex} skipped (slot={cam.Slot})");
                continue;
            }
            if (!_sessionsById.TryGetValue(cam.NvrId, out var session))
            {
                _audit.Write(AuditEventType.AppStarted, source: "config",
                    detail: $"camera nvr={cam.NvrId} idx={cam.CameraIndex} skipped: NVR not configured");
                continue;
            }
            try
            {
                var camera = session.RegisterTile(cam.CameraIndex, cam.StreamId, cam.Label);
                _viewModel.BindCameraToSlot(cam.Slot, camera);
                // NvrTitle is persisted on CameraSlotSettings (filled by Setup's
                // Pull-names sync) so the tile shows the recorder-side name from
                // the first frame, no live device-status round-trip needed.
                // v2.0: AiEnabled and AiClasses also pushed from config to tile so
                // per-camera AI settings take effect from the first frame after a
                // restart, no Setup-window-open needed.
                if (cam.Slot >= 0 && cam.Slot < _viewModel.Tiles.Count)
                {
                    var t = _viewModel.Tiles[cam.Slot];
                    t.NvrTitle = cam.NvrTitle ?? "";
                    t.AiEnabled = cam.AiEnabled;
                    t.AiClasses = cam.AiClasses;
                    t.MaskPaddingPercent = cam.MaskPaddingPercent;
                    t.AiRoiPolygon = cam.AiRoiPolygon;
                    t.AiConfidencePercent = cam.AiConfidencePercent;
                    t.AiUseSourceBlur = cam.AiUseSourceBlur;
                    t.AiMaskOpacity = Math.Clamp(cam.AiMaskOpacityPercent, 20, 100) / 100.0;
                }
                _bindingsBySlot[cam.Slot] = (cam.NvrId, cam.CameraIndex);
            }
            catch (Exception ex)
            {
                _audit.Write(AuditEventType.AppStarted, source: "config",
                    detail: $"camera nvr={cam.NvrId} idx={cam.CameraIndex} (slot {cam.Slot}) skipped: {ex.Message}");
            }
        }

        // In privacy-default mode the grid always boots with everyone blurred,
        // regardless of what state.json says. Reveals never survive a restart;
        // that's part of the "privacy is the default" contract. RestoreState
        // would reapply yesterday's reveal, which would defeat the mode.
        if (_privacy.Mode == PrivacyMode.PrivacyDefault)
        {
            _maskController.PrimePrivacyDefaultState(_viewModel.Tiles);
        }
        else
        {
            RestoreState();
        }

        // Push the current overlay toggles onto the freshly-bound tiles so the
        // bottom bar reflects the persisted Setup choice from the first frame.
        ApplyTileOverlayToggles();

        // Kick off v2.0 AI detector init in the background. Tiles get AttachDetector
        // once the model is loaded; until then their Detections stay empty (which
        // renders as nothing on top of the live image). On init failure the app
        // keeps running with v1.x masking only - no privacy regression.
        _ = InitializeAiDetectorAsync();

        // Start the IPC broadcaster after the grid is fully wired so the very first
        // Hello snapshot already reflects any restored masks.
        _ipcBroadcaster = new IpcStateBroadcaster(
            _viewModel, _bindingsBySlot, _maskController, _pinService, Dispatcher);
        _ipcBroadcaster.Start();

        // Connect every configured NVR. Each session manages its own retry.
        foreach (var nvr in settings.Nvrs)
        {
            if (!_sessionsById.TryGetValue(nvr.Id, out var session)) continue;
            var conn = new NvrConnectionInfo(nvr.Ip, nvr.Port, nvr.User, nvr.Password);
            session.Start(conn);
        }

        _timerTick.Start();
        ApplyKioskMode(settings.Kiosk.Enabled);
        _lastAppliedSettings = settings;
    }

    /// <summary>
    /// Build a new <see cref="NvrSession"/> for one configured NVR and wire its
    /// connect / disconnect / reconnect events into the per-NVR aggregation
    /// dictionaries. Called on startup and on the live-add path.
    /// </summary>
    private NvrSession CreateAndWireSession(NvrSettings nvr)
    {
        var session = new NvrSession();
        int nvrId = nvr.Id;
        string nvrName = string.IsNullOrEmpty(nvr.Name) ? $"NVR {nvr.Id + 1}" : nvr.Name;

        session.Log += msg => Dispatcher.BeginInvoke(
            () => _viewModel.StatusLine = $"[{nvrName}] {msg}");

        session.Connected += channel =>
        {
            _audit.Write(AuditEventType.NvrConnected, source: $"nvr:{nvrId}", detail: $"channel={channel}");
            Dispatcher.BeginInvoke(() =>
            {
                _disconnectLabelByNvr.Remove(nvrId);
                _nextReconnectByNvr.Remove(nvrId);
                UpdateConnectionBanner();
            });
        };

        session.Disconnected += reason =>
        {
            var label = DisconnectReasonText.ForUser(reason);
            _audit.Write(AuditEventType.NvrDisconnected, source: $"nvr:{nvrId}", detail: $"{reason} - {label}");
            Dispatcher.BeginInvoke(() =>
            {
                _disconnectLabelByNvr[nvrId] = label;
                // Mark only tiles bound to THIS NVR as disconnected; tiles on
                // other NVRs may still be live.
                foreach (var (slot, identity) in _bindingsBySlot)
                {
                    if (identity.NvrId != nvrId) continue;
                    if (slot < 0 || slot >= _viewModel.Tiles.Count) continue;
                    var tile = _viewModel.Tiles[slot];
                    if (tile.HasCamera) tile.SetDisconnectDetail(label);
                }
                _viewModel.StatusLine = $"[{nvrName}] {label}";
                UpdateConnectionBanner();
            });
        };

        session.ReconnectScheduled += nextAttemptUtc =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _nextReconnectByNvr[nvrId] = nextAttemptUtc;
                UpdateConnectionBanner();
            });
        };

        _sessionsById[nvrId] = session;
        return session;
    }

    /// <summary>
    /// Stand up (or tear down) the audit-syslog forwarder to match current
    /// settings. Called on startup and from the live-apply path; safe to call
    /// when the forwarder doesn't exist yet.
    /// </summary>
    private void ApplyAuditForwarder(AuditForwardSettings s)
    {
        var existing = _auditForwarder;
        _auditForwarder = null;
        _audit.SetForwarder(null);
        existing?.Dispose();

        if (!s.SyslogEnabled || string.IsNullOrWhiteSpace(s.SyslogHost)) return;

        _auditForwarder = new SyslogForwarder(s);
        _audit.SetForwarder(_auditForwarder);
    }

    private void ApplyKioskMode(bool enabled)
    {
        if (enabled && !_kioskGuard.IsActive)
        {
            _kioskGuard.Activate(this);
            _audit.Write(AuditEventType.AppStarted, source: "admin", detail: "kiosk mode enabled");
        }
        else if (!enabled && _kioskGuard.IsActive)
        {
            _kioskGuard.Deactivate();
            _audit.Write(AuditEventType.AppStarted, source: "admin", detail: "kiosk mode disabled");
        }
    }

    private void OnTileTapped(object sender, MouseButtonEventArgs e)
    {
        // Long-press already fired the mass action; eat this MouseLeftButtonUp
        // so it doesn't double-trigger as a single-tap toggle.
        if (_longPressFired)
        {
            _longPressFired = false;
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement fe && fe.DataContext is TileViewModel tile)
        {
            _maskController?.HandleTileClick(tile);
            e.Handled = true;
        }
    }

    /// <summary>
    /// AI-reveal trigger (v2.0.x F2): click on the small "AI" pill in the top-
    /// left of a tile. Routes to MaskController which handles auth + duration
    /// pick + audit. e.Handled=true stops the click from bubbling to
    /// OnTileTapped (which would otherwise toggle the full-tile mask).
    /// </summary>
    private void OnAiRevealIconClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TileViewModel tile)
        {
            _maskController?.RequestAiReveal(tile);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Manual remask trigger (v2.0.x F2): click on the "AI off · {countdown}"
    /// badge in the top-right of a revealed tile. Cancels the active reveal
    /// immediately; audit reason "manual-remask". Same e.Handled rationale
    /// as OnAiRevealIconClick.
    /// </summary>
    private void OnAiRevealBadgeClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TileViewModel tile)
        {
            _maskController?.CancelAiReveal(tile, reason: "manual-remask");
            e.Handled = true;
        }
    }

    // ---- Long-press gesture (v1.3.0 item 1) -------------------------------
    //
    // 500 ms hold anywhere in the main grid triggers the mass mask/unmask
    // action via MaskController. Movement beyond the system drag threshold
    // or releasing before the timer ticks cancels the gesture and falls
    // back to ordinary single-tap behaviour on the tile under the cursor.

    // Single reusable timer instance. Allocating per click leaked when a
    // mouse-up was missed (modal/UAC/focus loss): a new mouse-down would
    // overwrite the field before StopLongPressTimer could shut the prior
    // timer down, leaving an orphan ticking in the background and re-firing
    // HandleLongPress every 500 ms.
    private readonly DispatcherTimer _longPressTimer = new(DispatcherPriority.Input)
    {
        Interval = TimeSpan.FromMilliseconds(500),
    };
    private bool _longPressTimerWired;
    private Point _longPressStart;
    private bool _longPressFired;

    private void OnRootMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Always stop first so a stuck timer from a previously missed
        // mouse-up doesn't double-fire on the next click.
        _longPressTimer.Stop();
        if (!_longPressTimerWired)
        {
            _longPressTimer.Tick += OnLongPressTick;
            _longPressTimerWired = true;
        }
        _longPressFired = false;
        _longPressStart = e.GetPosition(this);
        _longPressTimer.Start();
    }

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        if (!_longPressTimer.IsEnabled) return;
        var pt = e.GetPosition(this);
        if (Math.Abs(pt.X - _longPressStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pt.Y - _longPressStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }
        _longPressTimer.Stop();
    }

    private void OnRootMouseUp(object sender, MouseButtonEventArgs e)
    {
        _longPressTimer.Stop();
    }

    private void OnLongPressTick(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        if (_maskController is null) return;
        _longPressFired = true;
        _maskController.HandleLongPress(_viewModel.Tiles, _privacy.ShowMassUnmaskConfirm);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_maskController is null) return;
        // While a mass-mask hold is active the per-tile auto-unmask timers
        // must not advance; otherwise individual tiles would pop open one
        // by one while the caregiver is still away from the post.
        if (!_maskController.IsMassHoldActive)
        {
            // Direction of the per-tile auto-timer follows the configured
            // privacy mode: oversight-default counts down to "auto-unmask"
            // (mask -> visible), privacy-default counts down to "auto-re-mask"
            // (visible -> mask). Only one direction is ever live per tile.
            if (_privacy.Mode == PrivacyMode.OversightDefault)
            {
                foreach (var tile in _viewModel.Tiles)
                {
                    if (tile.TickAutoUnmask(_privacy.WarningMinutes))
                    {
                        _maskController.AutoExpireMask(tile);
                    }
                }
            }
            else
            {
                foreach (var tile in _viewModel.Tiles)
                {
                    if (tile.TickAutoReMask(_privacy.WarningMinutes))
                    {
                        _maskController.AutoReMaskTile(tile);
                    }
                }
            }
        }

        if (_viewModel.ConnectionLost) UpdateConnectionBanner();
    }

    /// <summary>
    /// <summary>
    /// Push the current ShowCameraLabel / ShowNvrTitle privacy toggles onto
    /// every tile so the bottom-bar overlay reflects them without restart.
    /// Called on initial load and after a Setup save.
    /// </summary>
    private void ApplyTileOverlayToggles()
    {
        foreach (var tile in _viewModel.Tiles)
        {
            tile.ShowLabel = _privacy.ShowCameraLabel;
            tile.ShowNvrTitle = _privacy.ShowNvrTitle;
        }
    }

    /// <summary>
    /// Refresh the disconnect banner text from current state. Called once on
    /// disconnect/schedule and then every tick while disconnected so the
    /// countdown ticks down.
    /// </summary>
    private void UpdateConnectionBanner()
    {
        var s = Strings.Instance.Current;
        // Aggregate across all NVRs: any disconnect → banner on. The countdown
        // tracks the soonest scheduled reconnect across the fleet so the user
        // sees a sensible single timer.
        bool anyDisconnected = _disconnectLabelByNvr.Count > 0 || _nextReconnectByNvr.Count > 0;
        if (!anyDisconnected)
        {
            _viewModel.ConnectionLost = false;
            _viewModel.ConnectionBanner = "";
            return;
        }
        _viewModel.ConnectionLost = true;

        DateTime? nextAttempt = _nextReconnectByNvr.Values.Count > 0 ? _nextReconnectByNvr.Values.Min() : (DateTime?)null;
        if (nextAttempt is null)
        {
            _viewModel.ConnectionBanner = s.ConnectionAttempting;
            return;
        }
        var remaining = nextAttempt.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _viewModel.ConnectionBanner = s.ConnectionAttempting;
            return;
        }
        int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        // Pick the most-recent disconnect label as a representative reason; with
        // multiple NVRs offline at once we show one of them rather than a
        // running list (the per-tile status bar shows the per-NVR detail).
        string reason = _disconnectLabelByNvr.Values.LastOrDefault() ?? s.ConnectionLost;
        _viewModel.ConnectionBanner = string.Format(s.ConnectionLostFormat, reason, seconds);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestExit();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void OnContextMenuRequested(object sender, MouseButtonEventArgs e)
    {
        // Build the kiosk menu on demand so labels honour the current language.
        var s = Strings.Instance.Current;
        var menu = new ContextMenu { PlacementTarget = this };
        var help = new MenuItem { Header = s.MenuHelp };
        help.Click += (_, _) => OnHelpRequested();
        var setup = new MenuItem { Header = s.MenuSetup };
        setup.Click += (_, _) => OnSetupRequested();
        var exit = new MenuItem { Header = s.MenuExit };
        exit.Click += (_, _) => RequestExit();
        menu.Items.Add(help);
        menu.Items.Add(setup);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// Open the in-app help. Available without admin-PIN — there is nothing
    /// destructive in the help screen and caregivers should be able to consult
    /// it at any time.
    /// </summary>
    private void OnHelpRequested()
    {
        try
        {
            var help = new HelpWindow { Owner = this };
            help.ShowDialog();
        }
        catch (Exception ex)
        {
            _viewModel.StatusLine = $"Help kon niet worden geopend: {ex.Message}";
        }
    }

    private void OnSetupRequested()
    {
        var s = Strings.Instance.Current;
        if (!VerifyAdminPin(s.AdminPinSetupOpenTitle, s.AdminPinSetupOpenSub)) return;

        // Lambda captures _sessionsById so Setup can ask the live, already-
        // authenticated NvrSession for camera names instead of opening a
        // second concurrent session against the NVR (which most IDIS NVRs
        // refuse on the same user).
        var setup = new SetupWindow(_configService, _adminPin, _audit,
            cameraNameFetcher: async (nvrId, ct) =>
            {
                if (!_sessionsById.TryGetValue(nvrId, out var session))
                {
                    throw new InvalidOperationException(
                        $"NVR id={nvrId} is not currently connected.");
                }
                return await session.FetchCameraNamesAsync(TimeSpan.FromSeconds(8), ct).ConfigureAwait(true);
            },
            // Apply-button path: the dialog stays open but we still want
            // MainWindow to pick up the new config so the operator can see
            // their changes live without closing Setup first.
            onApplied: ApplyChangedSettings,
            // ROI editor snapshot source: clone + freeze the current tile bitmap
            // so the editor sees a stable frame even while the live feed keeps
            // updating in the background.
            frameSnapshotProvider: CaptureSlotSnapshot,
            // Live AI runtime info source for the About tab's System capabilities
            // card (status + model description, sampled when the card renders).
            aiRuntimeProvider: SnapshotAiRuntime)
        { Owner = this };
        setup.ShowDialog();
        // The Save path also fires onApplied (via SaveAndApply), so when the
        // dialog finally closes the live state is already current. Calling
        // ApplyChangedSettings again here would be redundant.
    }

    /// <summary>
    /// Returns the current AI detector's runtime info for the About tab's
    /// "System capabilities" card. Returns a "no detector" snapshot when the
    /// AI path is not wired (e.g. init failed, Tier 0 host).
    /// </summary>
    private AiRuntimeInfo SnapshotAiRuntime()
    {
        var detector = _aiDetector;
        if (detector is null)
        {
            return new AiRuntimeInfo(Status: "Unavailable", ModelDescription: null);
        }
        return new AiRuntimeInfo(
            Status: detector.Status.ToString(),
            ModelDescription: detector.Capability.ModelDescription);
    }

    /// <summary>
    /// Captures a frozen snapshot of the live tile at the given slot. Used by
    /// the ROI editor in Setup so the polygon can be drawn over a representative
    /// frame even while the underlying WriteableBitmap keeps receiving GDK
    /// updates. Returns null when the slot is out of range or has no frame yet.
    /// </summary>
    private System.Windows.Media.Imaging.BitmapSource? CaptureSlotSnapshot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _viewModel.Tiles.Count) return null;
        var bmp = _viewModel.Tiles[slotIndex].Bitmap;
        if (bmp is null || bmp.PixelWidth == 0 || bmp.PixelHeight == 0) return null;
        try
        {
            var clone = bmp.Clone();
            clone.Freeze();
            return clone;
        }
        catch
        {
            // Snapshot is best-effort. If Clone fails (e.g. format mismatch) the
            // ROI editor falls back to its "no live frame available" placeholder.
            return null;
        }
    }

    /// <summary>
    /// Reload settings after the user saved in the setup window. Trivial changes
    /// (language, privacy timers, session-PIN policy, camera labels) are applied
    /// in-place; structural changes (NVR connection, camera bindings, grid size)
    /// require a full reinit and prompt the user.
    /// </summary>
    private void ApplyChangedSettings()
    {
        var newSettings = _configService.Load();
        bool structuralChange = HasStructuralChange(newSettings);

        // 1) Language: Strings.Instance fires PropertyChanged that bound XAML and
        //    TileViewModel subscribers pick up live.
        Strings.Instance.Apply(newSettings.Language);

        // 2) Privacy timers + session-PIN policy + AD policy: stored in _privacy /
        //    _auth, read lazily by the controller, so just swap the reference.
        _privacy = newSettings.Privacy;
        _auth = newSettings.Auth;
        // 2a) Tile-overlay toggles (custom label / NVR title visibility) are
        //     pushed onto every tile so the bottom bar updates without restart.
        ApplyTileOverlayToggles();
        // 2b) Audit forwarding: hot-swap so a config change reaches the SIEM
        //     without restart. The bounded queue is recreated by SyslogForwarder.
        if (!AuditForwardEqual(_auditForward, newSettings.AuditForward))
        {
            _auditForward = newSettings.AuditForward;
            ApplyAuditForwarder(_auditForward);
        }

        // 3) Kiosk mode toggles can be installed/uninstalled live without restart.
        ApplyKioskMode(newSettings.Kiosk.Enabled);

        // 3) Camera-list change is live-applied: NvrSession reconciles the diff
        //    against the GDK subscription, then we rebind tiles in the view model
        //    without losing the live image on cameras that didn't change.
        if (!structuralChange)
        {
            ApplyCameraChangesLive(newSettings.Cameras);
            _lastAppliedSettings = newSettings;
            _viewModel.StatusLine = Strings.Instance.Current.SetupChangesApplied;
            return;
        }

        // 4) Structural changes need a clean reinit. We offer the user a choice
        //    rather than forcing it because they may want to keep the current
        //    NVR session alive until a quieter moment.
        var resp = MessageBox.Show(
            Strings.Instance.Current.SetupRestartPrompt,
            "InteractiveMask",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (resp == MessageBoxResult.Yes)
        {
            _authenticatedExit = true;
            Close();
        }
    }

    /// <summary>
    /// Reconcile the running sessions against a new camera list without a restart.
    /// With multi-NVR, the per-NVR target list determines what each session ends
    /// up subscribing to; bindings are slot-keyed so a camera moved between
    /// NVRs detaches from one session and attaches via another.
    /// </summary>
    private void ApplyCameraChangesLive(List<CameraSlotSettings> newCameras)
    {
        if (_sessionsById.Count == 0) return;

        // Step 1 — detach tiles whose binding (NvrId, CameraIndex) changed or
        // whose slot moved. The post-step 3 loop reattaches them at the new slot.
        var newBySlot = newCameras
            .Where(c => c.Slot >= 0)
            .GroupBy(c => c.Slot)
            .ToDictionary(g => g.Key, g => g.First());

        var oldEntries = _bindingsBySlot.ToList();
        foreach (var (oldSlot, identity) in oldEntries)
        {
            bool keep = newBySlot.TryGetValue(oldSlot, out var newCam)
                        && newCam!.NvrId == identity.NvrId
                        && newCam.CameraIndex == identity.CameraIndex;
            if (!keep)
            {
                if (oldSlot >= 0 && oldSlot < _viewModel.Tiles.Count)
                {
                    // If the tile being detached has an active AI-reveal, end it
                    // first so the audit trail records that the reveal window was
                    // cut by a camera rebinding rather than disappearing silently.
                    var oldTile = _viewModel.Tiles[oldSlot];
                    if (oldTile.IsAiRevealed)
                    {
                        _maskController?.CancelAiReveal(oldTile, reason: "tile-rebound");
                    }
                    oldTile.Detach();
                }
                _bindingsBySlot.Remove(oldSlot);
            }
        }

        // Step 2 — recompute the per-NVR target list and ask each session to
        // reconcile. Cameras in the new list that point at an unknown NVR id
        // are silently skipped (the user never reaches that state via Setup;
        // it would only happen with a hand-edited config).
        var grouped = newCameras
            .Where(c => _sessionsById.ContainsKey(c.NvrId))
            .GroupBy(c => c.NvrId)
            .ToDictionary(g => g.Key, g => g.ToList());

        int totalAdded = 0, totalRemoved = 0;
        foreach (var (nvrId, session) in _sessionsById)
        {
            grouped.TryGetValue(nvrId, out var camsForThisNvr);
            var diff = session.UpdateCameras(
                (camsForThisNvr ?? new()).Select(c => (c.CameraIndex, c.StreamId, c.Label)));
            totalAdded += diff.Added.Count;
            totalRemoved += diff.Removed.Count;
        }

        _audit.Write(AuditEventType.AppStarted, source: "config",
            detail: $"camera list updated: +{totalAdded} / -{totalRemoved}");

        // Snapshot the previous per-slot camera config so step 4 can skip tiles
        // whose Setup state didn't actually change. Without this every Apply
        // re-pushed Label / NvrTitle / AI settings to every tile, which (a)
        // re-allocated the AiClasses / AiRoiPolygon collection references every
        // time, (b) triggered an unconditional MaskOverlayText + IsLabelVisible
        // property-change ping on every tile via UpdateLabel, and (c) re-fired
        // the AI-icon visibility MultiDataTrigger across the grid. Cheap to
        // compare; saves the work for tiles that didn't change.
        var prevBySlot = _lastAppliedSettings?.Cameras
            .Where(c => c.Slot >= 0)
            .GroupBy(c => c.Slot)
            .ToDictionary(g => g.Key, g => g.First())
            ?? new();

        // Track slots that got their state freshly pushed in step 3 so step 4
        // doesn't redundantly write the same values a second time on the same
        // Apply pass.
        var freshlyBound = new HashSet<int>();

        // Step 3 — bind every camera in the new list to its slot. Cameras that
        // were already bound at the right slot are skipped so the live image
        // stays uninterrupted.
        foreach (var cam in newCameras)
        {
            if (cam.Slot < 0 || cam.Slot >= _viewModel.Tiles.Count) continue;
            if (_bindingsBySlot.ContainsKey(cam.Slot)) continue;
            if (!_sessionsById.TryGetValue(cam.NvrId, out var session)) continue;
            if (!session.Tiles.TryGetValue(cam.CameraIndex, out var cameraTile)) continue;

            _viewModel.BindCameraToSlot(cam.Slot, cameraTile);
            _bindingsBySlot[cam.Slot] = (cam.NvrId, cam.CameraIndex);
            if (cam.Slot >= 0 && cam.Slot < _viewModel.Tiles.Count)
            {
                var t = _viewModel.Tiles[cam.Slot];
                t.NvrTitle = cam.NvrTitle ?? "";
                t.AiEnabled = cam.AiEnabled;
                t.AiClasses = cam.AiClasses;
                t.MaskPaddingPercent = cam.MaskPaddingPercent;
                t.AiRoiPolygon = cam.AiRoiPolygon;
                t.AiConfidencePercent = cam.AiConfidencePercent;
                t.AiUseSourceBlur = cam.AiUseSourceBlur;
                t.AiMaskOpacity = Math.Clamp(cam.AiMaskOpacityPercent, 20, 100) / 100.0;
                freshlyBound.Add(cam.Slot);

                // If the operator just turned AI off on a tile that still had
                // an active reveal window running, the reveal becomes nonsense
                // (there's no AI overlay to reveal). End it cleanly so the
                // badge clears and the audit trail records the supersession.
                if (!t.AiEnabled && t.IsAiRevealed)
                {
                    _maskController?.CancelAiReveal(t, reason: "ai-disabled");
                }
            }
        }

        // F3: the eligible-streams set may have changed (operator toggled AI
        // on/off on one or more cameras). Push the new set to the adaptive
        // controller so its disable-victim logic reads from the current grid.
        RefreshDegradationEligibleStreams();

        // Step 4 — refresh labels, NVR titles and v2.0 AI settings for cameras
        // whose only change was a non-rebind field. Toggling AI on/off or changing
        // categories in Setup takes effect immediately on Apply without needing
        // a restart. Skipped for slots that step 3 just freshly bound, and for
        // slots whose config is identical to last Apply (no work, no INPC churn).
        foreach (var cam in newCameras)
        {
            if (cam.Slot < 0 || cam.Slot >= _viewModel.Tiles.Count) continue;
            if (freshlyBound.Contains(cam.Slot)) continue;
            if (prevBySlot.TryGetValue(cam.Slot, out var prev) && CameraConfigUnchanged(prev, cam))
                continue;

            var t = _viewModel.Tiles[cam.Slot];
            t.UpdateLabel(cam.Label);
            t.NvrTitle = cam.NvrTitle ?? "";
            t.AiEnabled = cam.AiEnabled;
            t.AiClasses = cam.AiClasses;
            t.MaskPaddingPercent = cam.MaskPaddingPercent;
            t.AiRoiPolygon = cam.AiRoiPolygon;
            t.AiConfidencePercent = cam.AiConfidencePercent;
            t.AiUseSourceBlur = cam.AiUseSourceBlur;
            t.AiMaskOpacity = Math.Clamp(cam.AiMaskOpacityPercent, 20, 100) / 100.0;
        }
    }

    /// <summary>
    /// True when every field that step 4 of ApplyCameraChangesLive would push
    /// to the TileViewModel is identical between two camera-slot configs.
    /// Collection fields (AiClasses, AiRoiPolygon) are compared by reference
    /// first as a fast path, then by deep equality; SetupWindow rebuilds these
    /// per Save, so equal contents in different instances are the common case
    /// when only an unrelated tile was edited.
    /// </summary>
    private static bool CameraConfigUnchanged(CameraSlotSettings a, CameraSlotSettings b)
    {
        if (a.Label != b.Label) return false;
        if ((a.NvrTitle ?? "") != (b.NvrTitle ?? "")) return false;
        if (a.AiEnabled != b.AiEnabled) return false;
        if (a.MaskPaddingPercent != b.MaskPaddingPercent) return false;
        if (a.AiConfidencePercent != b.AiConfidencePercent) return false;
        if (a.AiUseSourceBlur != b.AiUseSourceBlur) return false;
        if (a.AiMaskOpacityPercent != b.AiMaskOpacityPercent) return false;

        if (!ReferenceEquals(a.AiClasses, b.AiClasses)
            && !a.AiClasses.SetEquals(b.AiClasses)) return false;

        if (!ReferenceEquals(a.AiRoiPolygon, b.AiRoiPolygon))
        {
            if (a.AiRoiPolygon.Count != b.AiRoiPolygon.Count) return false;
            for (int i = 0; i < a.AiRoiPolygon.Count; i++)
            {
                if (a.AiRoiPolygon[i].X != b.AiRoiPolygon[i].X) return false;
                if (a.AiRoiPolygon[i].Y != b.AiRoiPolygon[i].Y) return false;
            }
        }

        return true;
    }

    private static bool AuditForwardEqual(AuditForwardSettings a, AuditForwardSettings b) =>
        a.SyslogEnabled == b.SyslogEnabled &&
        a.SyslogHost == b.SyslogHost &&
        a.SyslogPort == b.SyslogPort &&
        a.SyslogProtocol == b.SyslogProtocol &&
        a.SyslogFacility == b.SyslogFacility &&
        a.SyslogAppName == b.SyslogAppName;

    /// <summary>
    /// True when at least one of the settings that requires reconnecting to the
    /// NVR or rebuilding the tile grid has changed. Camera list changes are
    /// NOT structural anymore — they're live-applied via NvrSession.UpdateCameras
    /// + a ViewModel rebind, so the user keeps the live image during the change.
    /// </summary>
    private bool HasStructuralChange(AppSettings updated)
    {
        if (_lastAppliedSettings is null || _sessionsById.Count == 0) return true;

        // Any change to the NVR fleet (added, removed, or any connection field
        // changed for an existing NVR) is structural. We could in theory live-add
        // or live-remove a session, but doing it transactionally with the camera
        // bindings is fiddly enough that a clean restart is the safer default.
        var oldById = _lastAppliedSettings.Nvrs.ToDictionary(n => n.Id);
        var newById = updated.Nvrs.ToDictionary(n => n.Id);
        if (oldById.Count != newById.Count) return true;
        foreach (var (id, oldNvr) in oldById)
        {
            if (!newById.TryGetValue(id, out var newNvr)) return true;
            if (oldNvr.Ip != newNvr.Ip ||
                oldNvr.Port != newNvr.Port ||
                oldNvr.User != newNvr.User ||
                oldNvr.Password != newNvr.Password ||
                oldNvr.Name != newNvr.Name)
                return true;
        }

        if (_viewModel.Rows != updated.Grid.Rows || _viewModel.Columns != updated.Grid.Columns)
            return true;

        // Privacy mode flip is structural: oversight-default and privacy-default
        // disagree on what the boot state of every tile is, what auto-timer
        // direction means, and whether Restored state should be applied. The
        // safest live-apply for a mode flip is "no live-apply" — restart.
        if (_lastAppliedSettings.Privacy.Mode != updated.Privacy.Mode)
            return true;

        return false;
    }

    private void RequestExit()
    {
        var s = Strings.Instance.Current;
        if (!VerifyAdminPin(s.AdminPinExitTitle, s.AdminPinExitSub)) return;
        _authenticatedExit = true;
        Close();
    }

    private bool VerifyAdminPin(string title, string subtitle)
    {
        if (!_adminPin.IsConfigured) return true; // shouldn't happen post-bootstrap

        bool ok = PinDialog.PromptToVerify(
            owner: this,
            verifier: pin =>
            {
                bool valid = _adminPin.Verify(pin);
                if (!valid) _audit.Write(AuditEventType.PinFail, source: "admin", detail: title);
                return valid;
            },
            // Admin PIN does not share the session-PIN lockout; we just track
            // each failed attempt in the audit log. A future hardening pass can
            // add its own lockout window if needed.
            lockoutCheck: () => TimeSpan.Zero,
            title: title,
            subtitle: subtitle);
        return ok;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Unauthenticated close attempts (Alt+F4, OS task-end via menu) must
        // still go through admin-PIN. OS-level shutdown/logoff bypasses this
        // cancel because it happens before OnClosing in those code paths.
        if (!_authenticatedExit && _adminPin.IsConfigured)
        {
            var s = Strings.Instance.Current;
            if (!VerifyAdminPin(s.AdminPinExitTitle, s.AdminPinExitSub))
            {
                e.Cancel = true;
                return;
            }
            _authenticatedExit = true;
        }

        _kioskGuard.Dispose();
        _timerTick.Stop();
        _stateStore.Flush();
        _ipcBroadcaster?.Dispose();
        _ipcBroadcaster = null;
        // Detach the AI detector from each tile before disposing so any in-flight
        // SubmitDetectionAsync calls won't try to assign back to a torn-down VM.
        foreach (var tile in _viewModel.Tiles)
        {
            tile.AttachDetector(null);
            tile.Degradation = null;       // F3: drop the controller reference
        }
        if (_aiDetector is OnnxLocalDetector localBeforeDispose
            && localBeforeDispose.Degradation is { } degBefore)
        {
            // F3: unsubscribe so the controller can't fire StateChanged into
            // the about-to-be-torn-down window once dispose returns.
            degBefore.StateChanged -= OnDegradationStateChanged;
        }
        if (_aiDetector != null)
        {
            try
            {
                // Run the async dispose on the thread pool so it can't capture and
                // deadlock against the UI dispatcher, then wait at most 2 seconds.
                // ONNX session dispose is sub-second; the timeout is purely a safety
                // net so a future remote-detector backend can't strand the shutdown.
                Task.Run(() => _aiDetector.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(2));
                _audit.Write(AuditEventType.AiDetectorStopped, source: "ai-detector",
                    detail: "graceful dispose");
            }
            catch (Exception ex)
            {
                // Best-effort log; shutdown continues regardless. If we couldn't
                // dispose cleanly the support engineer at least sees that we
                // tried to, with the reason it failed.
                try
                {
                    _audit.Write(AuditEventType.AiDetectorStopped, source: "ai-detector",
                        detail: $"dispose error: {ex.GetType().Name}: {ex.Message}");
                }
                catch { }
            }
            _aiDetector = null;
        }
        foreach (var session in _sessionsById.Values) session.Dispose();
        _sessionsById.Clear();
        _gdkLifetime?.Dispose();
        _gdkLifetime = null;
        _audit.Write(AuditEventType.AppStopped, source: "local");
        // Drain the syslog queue (best-effort) so the AppStopped event reaches
        // the SIEM before we exit.
        _audit.SetForwarder(null);
        _auditForwarder?.Dispose();
        _auditForwarder = null;
        // Flush + close the persistent audit-log handle. The final
        // AppStopped event just above is in the buffer until Dispose flushes,
        // so this is the durability point for the close-out record.
        _audit.Dispose();
    }

    /// <summary>
    /// Loads the YuNet face detector via ONNX Runtime + DirectML and, on success,
    /// attaches it to every tile so per-frame detection submission begins on the
    /// next frame. Init failure is non-fatal: tiles continue showing the live
    /// image plus v1.x manual masking, just without AI overlays.
    /// </summary>
    private async Task InitializeAiDetectorAsync()
    {
        try
        {
            var detector = new OnnxLocalDetector();
            var config = new DetectorConfig(
                EnabledClasses: new HashSet<ObjectClass>
                {
                    ObjectClass.Person,
                    ObjectClass.TwoWheeler,
                    ObjectClass.Vehicle,
                },
                ConfidenceThresholds: new Dictionary<ObjectClass, float>
                {
                    // Coordinator threshold is the FLOOR below which detections
                    // never reach the per-tile filter. Per-tile thresholds (from
                    // CameraSlotSettings.AiConfidencePercent, 15..70 in Setup)
                    // are applied on top. Keeping the coordinator floor at 0.15
                    // means a per-tile setting of 15% has actual effect; raising
                    // the coordinator floor would clip the bottom of that range.
                    [ObjectClass.Person]     = 0.15f,
                    [ObjectClass.TwoWheeler] = 0.15f,
                    [ObjectClass.Vehicle]    = 0.15f,
                },
                MaxQueueDepth: 1,
                PreferPolygonMasks: false);
            await detector.InitializeAsync(config).ConfigureAwait(true);

            _aiDetector = detector;
            foreach (var tile in _viewModel.Tiles)
            {
                tile.AttachDetector(detector);
                // F3: every tile reads the same DegradationController so the
                // adaptive frame-skip multiplier and the disabled-streams
                // set apply uniformly across the grid.
                tile.Degradation = detector.Degradation;
            }

            // F3: subscribe to degradation transitions for audit + the
            // per-tile "AI suspended by load" badge.
            if (detector.Degradation is not null)
            {
                detector.Degradation.StateChanged += OnDegradationStateChanged;
                RefreshDegradationEligibleStreams();
            }

            _audit.Write(AuditEventType.AiDetectorInit, source: "ai-detector",
                detail: $"{detector.Capability.BackendName}: {detector.Capability.ModelDescription}");
        }
        catch (Exception ex)
        {
            _audit.Write(AuditEventType.AiDetectorFault, source: "ai-detector",
                detail: $"init failed, falling back to v1.x masking: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// F3: handle a degradation-state transition raised by
    /// <see cref="DegradationController"/>. Two side effects:
    /// 1. Update each tile's <see cref="TileViewModel.IsAiSuspendedByLoad"/>
    ///    so the on-tile badge appears / disappears.
    /// 2. Write an <see cref="AuditEventType.AiDetectorDegraded"/> or
    ///    <see cref="AuditEventType.AiDetectorRestored"/> event with the
    ///    transition detail.
    /// Marshalled onto the UI thread; the controller raises from whatever
    /// thread did the latency feed.
    /// </summary>
    private void OnDegradationStateChanged(object? sender, DegradationStateChange e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnDegradationStateChanged(sender, e));
            return;
        }

        // Update tile badges. A tile is "suspended by load" iff its slot
        // index is in the disabled-streams set right now.
        foreach (var tile in _viewModel.Tiles)
        {
            tile.IsAiSuspendedByLoad = e.DisabledStreams.Contains(tile.SlotIndex);
        }

        // Audit. The detail line is compact but self-describing so support can
        // grep audit.log for "GlobalFallback" or "p95=" without guesswork.
        string detail = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0} -> {1}; mult {2} -> {3}; disabled={4}; p95={5:0}ms",
            e.FromState, e.ToState,
            e.FromMultiplier, e.ToMultiplier,
            e.DisabledStreams.Count,
            e.P95LatencyMs);

        var type = e.Direction == DegradationDirection.Escalate
            ? AuditEventType.AiDetectorDegraded
            : AuditEventType.AiDetectorRestored;
        _audit.Write(type, source: "ai-detector", detail: detail);
    }

    /// <summary>
    /// F3: feed the controller the current set of streams that are AI-eligible
    /// (i.e. operator has AiEnabled=true and a camera is bound). Called from
    /// post-init and from Setup → Apply.
    /// </summary>
    private void RefreshDegradationEligibleStreams()
    {
        // The controller is only attached to the local-ONNX backend; remote
        // backends (v2.1 Jetson sidecar) run their own degradation logic and
        // don't expose this surface yet.
        var degradation = (_aiDetector as OnnxLocalDetector)?.Degradation;
        if (degradation is null) return;

        var eligible = new List<int>();
        foreach (var tile in _viewModel.Tiles)
        {
            if (tile.HasCamera && tile.AiEnabled) eligible.Add(tile.SlotIndex);
        }
        degradation.SetEligibleStreams(eligible);
    }

    /// <summary>
    /// Build a snapshot of the current mask + PIN state and ask the StateStore to
    /// persist it (debounced). Called by the controller after every state change.
    /// </summary>
    private void SaveState()
    {
        var tiles = new List<PersistedTile>();
        foreach (var tile in _viewModel.Tiles)
        {
            if (!tile.HasCamera) continue;
            int cameraIndex = -1;
            if (_bindingsBySlot.TryGetValue(tile.SlotIndex, out var identity))
            {
                cameraIndex = identity.CameraIndex;
            }
            tiles.Add(new PersistedTile(
                Slot: tile.SlotIndex,
                CameraIndex: cameraIndex,
                IsMasked: tile.IsMasked,
                MaskedAtTicksUtc: tile.MaskedAtUtc?.Ticks,
                AutoUnmaskMinutes: tile.AutoUnmaskMinutes));
        }

        _stateStore.RequestSave(new PersistedState(
            ActivePin: _pinService.CurrentSessionPin,
            MaskedCount: _pinService.CurrentMaskedCount,
            Tiles: tiles));
    }

    /// <summary>
    /// On startup, replay the persisted mask state. Tiles are matched by slot
    /// (the user-facing position); if the slot no longer exists in the new
    /// config or the camera moved, the entry is dropped.
    /// </summary>
    private void RestoreState()
    {
        var state = _stateStore.Load();
        if (state is null) return;

        int restoredMasked = 0;
        foreach (var pt in state.Tiles)
        {
            int slot = pt.Slot;
            if (slot < 0 || slot >= _viewModel.Tiles.Count) continue;
            // Only restore if the slot is currently bound to a camera at all;
            // a re-config that emptied the slot drops the persisted mask.
            if (!_bindingsBySlot.ContainsKey(slot)) continue;
            if (slot < 0 || slot >= _viewModel.Tiles.Count) continue;
            var tile = _viewModel.Tiles[slot];
            if (pt.IsMasked)
            {
                var maskedAt = pt.MaskedAtTicksUtc is { } ticks ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;
                tile.SetMasked(true, pt.AutoUnmaskMinutes, maskedAt);
                restoredMasked++;
            }
        }

        // Restore the session PIN only if it covers the masks we actually restored.
        // If the config changed and fewer tiles were restored than persisted, we
        // still keep the PIN so the user can unlock the remaining masks.
        if (restoredMasked > 0 && !string.IsNullOrEmpty(state.ActivePin))
        {
            _pinService.Restore(state.ActivePin, restoredMasked);
        }
    }

    private AppSettings LoadSettings() => _configService.Load();
}
