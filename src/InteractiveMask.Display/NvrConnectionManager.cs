using InteractiveMask.Gdk;
using System.Windows.Threading;

namespace InteractiveMask.Display;

/// <summary>
/// Owns the multi-NVR GDK session fleet: one <see cref="NvrSession"/> per configured
/// NVR, slot bindings, disconnect/reconnect banner aggregation, and live camera-list
/// reconciliation. Extracted from <see cref="MainWindow"/> to keep the window class
/// focused on UI orchestration.
/// </summary>
public sealed class NvrConnectionManager
{
    private readonly DisplayViewModel _viewModel;
    private readonly AuditLog _audit;
    private readonly Dispatcher _dispatcher;
    private readonly Func<MaskController?> _maskControllerProvider;

    private readonly Dictionary<int, NvrSession> _sessionsById = new();
    private readonly Dictionary<int, (int NvrId, int CameraIndex)> _bindingsBySlot = new();
    private readonly Dictionary<int, string> _disconnectLabelByNvr = new();
    private readonly Dictionary<int, DateTime> _nextReconnectByNvr = new();

    public NvrConnectionManager(
        DisplayViewModel viewModel,
        AuditLog audit,
        Dispatcher dispatcher,
        Func<MaskController?> maskControllerProvider)
    {
        _viewModel = viewModel;
        _audit = audit;
        _dispatcher = dispatcher;
        _maskControllerProvider = maskControllerProvider;
    }

    /// <summary>Live NVR sessions keyed by configured NVR id.</summary>
    public IReadOnlyDictionary<int, NvrSession> SessionsById => _sessionsById;

    /// <summary>Per-grid-slot camera identity map used by IPC and state persistence.</summary>
    public IReadOnlyDictionary<int, (int NvrId, int CameraIndex)> BindingsBySlot => _bindingsBySlot;

    /// <summary>Mutable binding map for initial startup wiring from MainWindow.</summary>
    public Dictionary<int, (int NvrId, int CameraIndex)> Bindings => _bindingsBySlot;

    public void CreateSessionsForNvrs(IEnumerable<NvrSettings> nvrs)
    {
        foreach (var nvr in nvrs)
        {
            if (string.IsNullOrWhiteSpace(nvr.Ip) || string.IsNullOrWhiteSpace(nvr.Password)) continue;
            CreateAndWireSession(nvr);
        }
    }

    public void StartAllSessions(IEnumerable<NvrSettings> nvrs)
    {
        foreach (var nvr in nvrs)
        {
            if (!_sessionsById.TryGetValue(nvr.Id, out var session)) continue;
            var conn = new NvrConnectionInfo(nvr.Ip, nvr.Port, nvr.User, nvr.Password);
            session.Start(conn);
        }
    }

    /// <summary>
    /// Build a new <see cref="NvrSession"/> for one configured NVR and wire its
    /// connect / disconnect / reconnect events into the per-NVR aggregation
    /// dictionaries. Called on startup and on the live-add path.
    /// </summary>
    public NvrSession CreateAndWireSession(NvrSettings nvr)
    {
        var session = new NvrSession();
        int nvrId = nvr.Id;
        string nvrName = string.IsNullOrEmpty(nvr.Name) ? $"NVR {nvr.Id + 1}" : nvr.Name;

        session.Log += msg => _dispatcher.BeginInvoke(
            () => _viewModel.StatusLine = $"[{nvrName}] {msg}");

        session.Connected += channel =>
        {
            _audit.Write(AuditEventType.NvrConnected, source: $"nvr:{nvrId}", detail: $"channel={channel}");
            _dispatcher.BeginInvoke(() =>
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
            _dispatcher.BeginInvoke(() =>
            {
                _disconnectLabelByNvr[nvrId] = label;
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
            _dispatcher.BeginInvoke(() =>
            {
                _nextReconnectByNvr[nvrId] = nextAttemptUtc;
                UpdateConnectionBanner();
            });
        };

        _sessionsById[nvrId] = session;
        return session;
    }

    /// <summary>
    /// Refresh the disconnect banner text from current state. Called once on
    /// disconnect/schedule and then every tick while disconnected so the
    /// countdown ticks down.
    /// </summary>
    public void UpdateConnectionBanner()
    {
        var s = Strings.Instance.Current;
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
        string reason = _disconnectLabelByNvr.Values.LastOrDefault() ?? s.ConnectionLost;
        _viewModel.ConnectionBanner = string.Format(s.ConnectionLostFormat, reason, seconds);
    }

    /// <summary>
    /// Reconcile the running sessions against a new camera list without a restart.
    /// </summary>
    public void ApplyCameraChangesLive(
        List<CameraSlotSettings> newCameras,
        AppSettings? lastApplied,
        Action refreshDegradationEligibleStreams)
    {
        if (_sessionsById.Count == 0) return;
        var maskController = _maskControllerProvider();

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
                    var oldTile = _viewModel.Tiles[oldSlot];
                    if (oldTile.IsAiRevealed)
                    {
                        maskController?.CancelAiReveal(oldTile, reason: "tile-rebound");
                    }
                    oldTile.Detach();
                }
                _bindingsBySlot.Remove(oldSlot);
            }
        }

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

        var prevBySlot = lastApplied?.Cameras
            .Where(c => c.Slot >= 0)
            .GroupBy(c => c.Slot)
            .ToDictionary(g => g.Key, g => g.First())
            ?? new();

        var freshlyBound = new HashSet<int>();

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
                if (t.IsAiRevealed && !cam.AiEnabled)
                {
                    maskController?.CancelAiReveal(t, reason: "ai-disabled");
                }

                t.NvrTitle = cam.NvrTitle ?? "";
                t.AiEnabled = cam.AiEnabled;
                t.AiClasses = cam.AiClasses;
                t.MaskPaddingPercent = cam.MaskPaddingPercent;
                t.AiRoiPolygon = cam.AiRoiPolygon;
                t.AiConfidencePercent = cam.AiConfidencePercent;
                t.AiUseSourceBlur = cam.AiUseSourceBlur;
                t.AiMaskOpacity = Math.Clamp(cam.AiMaskOpacityPercent, 20, 100) / 100.0;
                freshlyBound.Add(cam.Slot);
            }
        }

        refreshDegradationEligibleStreams();

        foreach (var cam in newCameras)
        {
            if (cam.Slot < 0 || cam.Slot >= _viewModel.Tiles.Count) continue;
            if (freshlyBound.Contains(cam.Slot)) continue;
            if (prevBySlot.TryGetValue(cam.Slot, out var prev) && CameraConfigUnchanged(prev, cam))
                continue;

            var t = _viewModel.Tiles[cam.Slot];
            if (t.IsAiRevealed && !cam.AiEnabled)
            {
                maskController?.CancelAiReveal(t, reason: "ai-disabled");
            }

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
    /// Reconcile the running set of <see cref="NvrSession"/>s against an updated NVR list.
    /// </summary>
    public void ReconcileNvrFleet(List<NvrSettings> updated, AppSettings? lastApplied)
    {
        var newById = updated
            .Where(n => !string.IsNullOrWhiteSpace(n.Ip) && !string.IsNullOrWhiteSpace(n.Password))
            .ToDictionary(n => n.Id);
        var oldById = lastApplied?.Nvrs.ToDictionary(n => n.Id)
            ?? new Dictionary<int, NvrSettings>();

        foreach (var nvrId in _sessionsById.Keys.ToList())
        {
            if (!newById.TryGetValue(nvrId, out var newNvr))
            {
                TearDownNvrSession(nvrId, reason: "nvr-removed");
                continue;
            }

            if (oldById.TryGetValue(nvrId, out var oldNvr) && NeedsReconnect(oldNvr, newNvr))
            {
                TearDownNvrSession(nvrId, reason: "nvr-credentials-changed");
            }
        }

        foreach (var nvr in updated)
        {
            if (_sessionsById.ContainsKey(nvr.Id)) continue;
            if (string.IsNullOrWhiteSpace(nvr.Ip) || string.IsNullOrWhiteSpace(nvr.Password)) continue;

            var session = CreateAndWireSession(nvr);
            var conn = new NvrConnectionInfo(nvr.Ip, nvr.Port, nvr.User, nvr.Password);
            session.Start(conn);
            _audit.Write(AuditEventType.AppStarted, source: $"nvr:{nvr.Id}",
                detail: "nvr-added (live-apply)");
        }
    }

    /// <summary>
    /// Detach every tile bound to the given NVR, dispose its session, and remove it.
    /// </summary>
    public void TearDownNvrSession(int nvrId, string reason)
    {
        if (!_sessionsById.TryGetValue(nvrId, out var session)) return;
        var maskController = _maskControllerProvider();

        var slotsToDetach = _bindingsBySlot
            .Where(kv => kv.Value.NvrId == nvrId)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var slot in slotsToDetach)
        {
            if (slot >= 0 && slot < _viewModel.Tiles.Count)
            {
                var tile = _viewModel.Tiles[slot];
                if (tile.IsAiRevealed)
                {
                    maskController?.CancelAiReveal(tile, reason: "nvr-removed");
                }
                tile.Detach();
            }
            _bindingsBySlot.Remove(slot);
        }

        _disconnectLabelByNvr.Remove(nvrId);
        _nextReconnectByNvr.Remove(nvrId);
        UpdateConnectionBanner();

        try { session.Dispose(); }
        catch (Exception ex)
        {
            DiagnosticLog.SwallowedException($"NvrConnectionManager.TearDownNvrSession nvr={nvrId}", ex);
            _audit.Write(AuditEventType.NvrDisconnected, source: $"nvr:{nvrId}",
                detail: $"{reason} dispose-error: {ex.GetType().Name}: {ex.Message}");
        }
        _sessionsById.Remove(nvrId);

        _audit.Write(AuditEventType.NvrDisconnected, source: $"nvr:{nvrId}", detail: reason);
    }

    public void DisposeAll()
    {
        foreach (var session in _sessionsById.Values)
        {
            try { session.Dispose(); }
            catch (Exception ex) { DiagnosticLog.SwallowedException("NvrConnectionManager.DisposeAll", ex); }
        }
        _sessionsById.Clear();
        _bindingsBySlot.Clear();
        _disconnectLabelByNvr.Clear();
        _nextReconnectByNvr.Clear();
    }

    private static bool NeedsReconnect(NvrSettings oldNvr, NvrSettings newNvr) =>
        oldNvr.Ip       != newNvr.Ip       ||
        oldNvr.Port     != newNvr.Port     ||
        oldNvr.User     != newNvr.User     ||
        oldNvr.Password != newNvr.Password;

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
}
