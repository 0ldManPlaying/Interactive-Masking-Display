using InteractiveMask.Gdk;
using Microsoft.Extensions.Configuration;
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
    private readonly Dictionary<int, int> _slotByCameraIndex = new();
    private readonly DispatcherTimer _timerTick = new() { Interval = TimeSpan.FromSeconds(1) };

    private readonly KioskGuard _kioskGuard = new();
    private MaskController? _maskController;
    private IDisposable? _gdkLifetime;
    private NvrSession? _session;
    private IpcStateBroadcaster? _ipcBroadcaster;
    private PrivacySettings _privacy = new();
    private AuthSettings _auth = new();
    private AppSettings? _lastAppliedSettings;
    private bool _authenticatedExit;
    private DateTime? _nextReconnectUtc;
    private string _lastDisconnectLabel = "";
    private SyslogForwarder? _auditForwarder;
    private AuditForwardSettings _auditForward = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _timerTick.Tick += OnTimerTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // First-launch admin-PIN bootstrap. Without an admin PIN configured, the
        // kiosk can be exited and reconfigured by anyone walking past — so we
        // refuse to start until one exists.
        if (!_adminPin.IsConfigured)
        {
            var s0 = Strings.Instance.Current;
            var newPin = PinDialog.PromptForNewPin(
                this,
                title: s0.AdminPinFirstSetupTitle,
                subtitle: s0.AdminPinFirstSetupSub);
            if (string.IsNullOrEmpty(newPin))
            {
                _authenticatedExit = true;
                Close();
                return;
            }
            _adminPin.SetPin(newPin);
            _audit.Write(AuditEventType.PinSet, source: "admin", detail: "admin-pin initial setup");
        }

        var settings = LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.Nvr.Ip) || string.IsNullOrWhiteSpace(settings.Nvr.Password))
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
            onStateChanged: SaveState);

        int rows = Math.Max(1, settings.Grid.Rows);
        int cols = Math.Max(1, settings.Grid.Columns);
        _viewModel.InitializeGrid(rows, cols, Dispatcher);

        _gdkLifetime = GdkLifetime.Acquire("InteractiveMask.Display");

        _session = new NvrSession();
        _session.Log += msg => Dispatcher.BeginInvoke(() => _viewModel.StatusLine = msg);
        _session.Connected += channel =>
        {
            _audit.Write(AuditEventType.NvrConnected, source: "local", detail: $"channel={channel}");
            Dispatcher.BeginInvoke(() =>
            {
                _nextReconnectUtc = null;
                _lastDisconnectLabel = "";
                _viewModel.ConnectionLost = false;
                _viewModel.ConnectionBanner = "";
            });
        };
        _session.Disconnected += reason =>
        {
            var label = DisconnectReasonText.ForUser(reason);
            _audit.Write(AuditEventType.NvrDisconnected, source: "local", detail: $"{reason} - {label}");
            Dispatcher.BeginInvoke(() =>
            {
                _lastDisconnectLabel = label;
                foreach (var tile in _viewModel.Tiles)
                {
                    if (tile.HasCamera) tile.SetDisconnectDetail(label);
                }
                _viewModel.StatusLine = label;
                _viewModel.ConnectionLost = true;
                UpdateConnectionBanner();
            });
        };
        _session.ReconnectScheduled += nextAttemptUtc =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _nextReconnectUtc = nextAttemptUtc;
                _viewModel.ConnectionLost = true;
                UpdateConnectionBanner();
            });
        };

        foreach (var cam in settings.Cameras)
        {
            if (cam.Slot < 0 || cam.Slot >= _viewModel.Tiles.Count) continue;
            var camera = _session.RegisterTile(cam.CameraIndex, cam.StreamId, cam.Label);
            _viewModel.BindCameraToSlot(cam.Slot, camera);
            _slotByCameraIndex[cam.CameraIndex] = cam.Slot;
        }

        RestoreState();

        // Start the IPC broadcaster after the grid is fully wired so the very first
        // Hello snapshot already reflects any restored masks.
        _ipcBroadcaster = new IpcStateBroadcaster(
            _viewModel, _slotByCameraIndex, _maskController, _pinService, Dispatcher);
        _ipcBroadcaster.Start();

        var conn = new NvrConnectionInfo(settings.Nvr.Ip, settings.Nvr.Port, settings.Nvr.User, settings.Nvr.Password);
        _session.Start(conn);
        _timerTick.Start();

        ApplyKioskMode(settings.Kiosk.Enabled);
        _lastAppliedSettings = settings;
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

    private void OnTileClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TileViewModel tile)
        {
            _maskController?.HandleTileClick(tile);
            e.Handled = true;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_maskController is null) return;
        foreach (var tile in _viewModel.Tiles)
        {
            if (tile.TickAutoUnmask(_privacy.WarningMinutes))
            {
                _maskController.AutoExpireMask(tile);
            }
        }

        if (_viewModel.ConnectionLost) UpdateConnectionBanner();
    }

    /// <summary>
    /// Refresh the disconnect banner text from current state. Called once on
    /// disconnect/schedule and then every tick while disconnected so the
    /// countdown ticks down.
    /// </summary>
    private void UpdateConnectionBanner()
    {
        var s = Strings.Instance.Current;
        if (_nextReconnectUtc is null)
        {
            _viewModel.ConnectionBanner = s.ConnectionAttempting;
            return;
        }
        var remaining = _nextReconnectUtc.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _viewModel.ConnectionBanner = s.ConnectionAttempting;
            return;
        }
        int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var reason = string.IsNullOrEmpty(_lastDisconnectLabel) ? s.ConnectionLost : _lastDisconnectLabel;
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

        var setup = new SetupWindow(_configService, _adminPin, _audit) { Owner = this };
        setup.ShowDialog();
        if (!setup.ConfigChanged) return;

        ApplyChangedSettings();
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
        // 2b) Audit forwarding: hot-swap so a config change reaches the SIEM
        //     without restart. The bounded queue is recreated by SyslogForwarder.
        if (!AuditForwardEqual(_auditForward, newSettings.AuditForward))
        {
            _auditForward = newSettings.AuditForward;
            ApplyAuditForwarder(_auditForward);
        }

        // 3) Kiosk mode toggles can be installed/uninstalled live without restart.
        ApplyKioskMode(newSettings.Kiosk.Enabled);

        // 3) Camera labels for the same camera index can be updated in place
        //    without reconnecting to the NVR.
        if (!structuralChange)
        {
            foreach (var cam in newSettings.Cameras)
            {
                if (!_slotByCameraIndex.TryGetValue(cam.CameraIndex, out var slot)) continue;
                if (slot < 0 || slot >= _viewModel.Tiles.Count) continue;
                _viewModel.Tiles[slot].UpdateLabel(cam.Label);
            }
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

    private static bool AuditForwardEqual(AuditForwardSettings a, AuditForwardSettings b) =>
        a.SyslogEnabled == b.SyslogEnabled &&
        a.SyslogHost == b.SyslogHost &&
        a.SyslogPort == b.SyslogPort &&
        a.SyslogProtocol == b.SyslogProtocol &&
        a.SyslogFacility == b.SyslogFacility &&
        a.SyslogAppName == b.SyslogAppName;

    /// <summary>
    /// True when at least one of the settings that requires reconnecting to the
    /// NVR or rebuilding the tile grid has changed.
    /// </summary>
    private bool HasStructuralChange(AppSettings updated)
    {
        // NVR connection
        var oldNvr = _session is null ? null : _lastAppliedSettings?.Nvr;
        if (oldNvr is null) return true;
        if (oldNvr.Ip != updated.Nvr.Ip ||
            oldNvr.Port != updated.Nvr.Port ||
            oldNvr.User != updated.Nvr.User ||
            oldNvr.Password != updated.Nvr.Password)
            return true;

        // Grid size
        if (_viewModel.Rows != updated.Grid.Rows || _viewModel.Columns != updated.Grid.Columns)
            return true;

        // Camera bindings (set of slot+cameraIndex+streamId)
        var oldKeys = _lastAppliedSettings?.Cameras
            .Select(c => $"{c.Slot}|{c.CameraIndex}|{c.StreamId}")
            .OrderBy(s => s)
            .ToList() ?? new List<string>();
        var newKeys = updated.Cameras
            .Select(c => $"{c.Slot}|{c.CameraIndex}|{c.StreamId}")
            .OrderBy(s => s)
            .ToList();
        return !oldKeys.SequenceEqual(newKeys);
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
        _session?.Dispose();
        _session = null;
        _gdkLifetime?.Dispose();
        _gdkLifetime = null;
        _audit.Write(AuditEventType.AppStopped, source: "local");
        // Drain the syslog queue (best-effort) so the AppStopped event reaches
        // the SIEM before we exit.
        _audit.SetForwarder(null);
        _auditForwarder?.Dispose();
        _auditForwarder = null;
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
            // Find the camera index this tile is bound to via the inverse map.
            int cameraIndex = -1;
            foreach (var kv in _slotByCameraIndex)
            {
                if (kv.Value == tile.SlotIndex) { cameraIndex = kv.Key; break; }
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
    /// On startup, replay the persisted mask state. Tiles are matched by camera index;
    /// if a previously-masked camera is no longer in the config the entry is dropped.
    /// </summary>
    private void RestoreState()
    {
        var state = _stateStore.Load();
        if (state is null) return;

        int restoredMasked = 0;
        foreach (var pt in state.Tiles)
        {
            if (!_slotByCameraIndex.TryGetValue(pt.CameraIndex, out var slot)) continue;
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
