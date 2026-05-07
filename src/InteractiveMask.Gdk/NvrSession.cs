#nullable enable
using GDK;
using System.Collections.Concurrent;

namespace InteractiveMask.Gdk;

/// <summary>
/// One connection to a single NVR. Owns the <c>g2watch</c> session and a shared
/// <c>g2decoder</c> with one slot per camera. Routes frame callbacks to the right
/// <see cref="CameraTile"/> based on <c>frame.channel</c>.
///
/// Lifetime:
///   1. <see cref="RegisterTile"/> for each camera you want to view (before Start).
///   2. <see cref="Start"/> to connect; tiles will start receiving frames as soon
///      as the GDK has subscribed and the camera produces data.
///   3. On disconnect, the session schedules an automatic reconnect after
///      <see cref="ReconnectDelay"/>.
///   4. <see cref="Dispose"/> tears everything down.
/// </summary>
public sealed class NvrSession : g2watch_listener, IDisposable
{
    private readonly g2watch _watch = new();
    private readonly g2decoder _decoder = new();
    private readonly object _decoderLock = new();
    // ConcurrentDictionary so the GDK callback thread (HandleFrame.TryGetValue)
    // and the UI thread (UpdateCameras / RegisterTile) don't race on the
    // dictionary itself. Lifetime of the values (CameraTile) is still owned
    // by NvrSession; CameraTile.Dispose serialises against in-flight frames
    // via its internal buffer lock.
    private readonly ConcurrentDictionary<int, CameraTile> _tilesByCamera = new();
    private readonly object _stateLock = new();

    private NvrConnectionInfo? _connection;
    private int _channel = -1;
    private bool _started;
    private bool _disposed;
    private CancellationTokenSource? _reconnectCts;

    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);

    public IReadOnlyDictionary<int, CameraTile> Tiles => _tilesByCamera;

    public event Action<int>? Connected;
    public event Action<G2DISCONNECT_REASON.TYPE>? Disconnected;
    public event Action<DateTime>? ReconnectScheduled;
    public event Action<string>? Log;

    /// <summary>
    /// Add a camera tile to the session. Must be called before <see cref="Start"/>;
    /// after start, the camera-list subscription is established once.
    /// </summary>
    public CameraTile RegisterTile(int cameraIndex, int streamId, string label)
    {
        if (_started) throw new InvalidOperationException("RegisterTile must be called before Start");
        var tile = new CameraTile(cameraIndex, streamId, label);
        if (!_tilesByCamera.TryAdd(cameraIndex, tile))
            throw new InvalidOperationException($"camera index {cameraIndex} already registered");
        return tile;
    }

    /// <summary>
    /// Live-apply a new camera list to a running session. Reconciles the diff:
    /// removed cameras are unsubscribed and disposed, new cameras are registered,
    /// and existing cameras get their stream / label updated in place. The GDK
    /// camera-list and stream-set subscriptions are re-pushed with force=true
    /// so the NVR sees the new selection without the kiosk being restarted.
    ///
    /// Returns the diff so callers (MainWindow) can update their view models.
    /// </summary>
    public CameraDiff UpdateCameras(IEnumerable<(int cameraIndex, int streamId, string label)> target)
    {
        var diff = new CameraDiff();
        if (_disposed) return diff;

        var targetList = target.ToList();
        var targetIndexes = new HashSet<int>(targetList.Select(t => t.cameraIndex));

        // Remove cameras no longer in target. CameraTile.Dispose serialises
        // against in-flight HandleFrame calls so this is safe even though the
        // GDK callback thread can be mid-decode for the very camera we drop.
        var toRemove = _tilesByCamera.Keys.Where(k => !targetIndexes.Contains(k)).ToList();
        foreach (var idx in toRemove)
        {
            if (_tilesByCamera.TryRemove(idx, out var tile))
            {
                tile.Dispose();
                diff.Removed.Add(idx);
            }
        }

        // Add new cameras and update existing ones in place.
        foreach (var (idx, stream, label) in targetList)
        {
            if (_tilesByCamera.TryGetValue(idx, out var existing))
            {
                existing.UpdateStreamAndLabel(stream, label);
            }
            else
            {
                var tile = new CameraTile(idx, stream, label);
                if (_tilesByCamera.TryAdd(idx, tile))
                {
                    diff.Added.Add(tile);
                }
                else
                {
                    // Lost the race against another caller — dispose the dup
                    // before it leaks its native buffers.
                    tile.Dispose();
                }
            }
        }

        // Re-push the subscription if we're connected. force=true so the NVR
        // applies the new set even if it already has a stale list cached.
        if (_channel >= 0 && _started)
        {
            var cameras = new g2channel_set();
            var streams = new g2channel_stream_set();
            foreach (var tile in _tilesByCamera.Values)
            {
                cameras.insert(tile.CameraIndex);
                streams.insert(tile.CameraIndex, tile.StreamId);
            }
            _watch.set_camera_list(_channel, cameras, force: true);
            _watch.set_camera_stream_set(_channel, streams, force: true);
        }

        return diff;
    }

    /// <summary>Result of a live <see cref="UpdateCameras"/> reconciliation.</summary>
    public sealed class CameraDiff
    {
        public List<CameraTile> Added { get; } = new();
        public List<int> Removed { get; } = new();
    }

    public void Start(NvrConnectionInfo connection)
    {
        if (_started) return;
        _started = true;
        _connection = connection;

        _watch.set_listener(this);
        _watch.startup(connections: 1, alive_check_interval: 60_000, focus: IntPtr.Zero);

        // One decoder slot per registered camera. Decoder_id = cameraIndex within
        // CameraTile.HandleFrame matches this layout.
        int slots = _tilesByCamera.Keys.DefaultIfEmpty(-1).Max() + 1;
        if (slots < 1) slots = 1;
        _decoder.startup(slots);

        ConnectInternal();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;

        if (_channel >= 0 && _watch.is_disconnectable(_channel))
        {
            _watch.disconnect(_channel);
        }
        _watch.cleanup();
        _decoder.cleanup();

        // Snapshot the tiles before clearing so we don't enumerate while disposing.
        var tiles = _tilesByCamera.Values.ToList();
        _tilesByCamera.Clear();
        foreach (var tile in tiles) tile.Dispose();
    }

    private void ConnectInternal()
    {
        if (_disposed || _connection is null) return;

        var ni = new G2NETWORK_INFO
        {
            _address = _connection.Ip,
            _user_id = _connection.User,
            _password = _connection.Password,
        };
        ni.set_port(G2NETWORK_INFO.PORT_TYPE.WATCH_PORT, (ushort)_connection.Port);

        foreach (var tile in _tilesByCamera.Values)
        {
            tile.SetStatus(TileStatus.Pending, null);
        }

        Log?.Invoke($"connect_ras {_connection.Ip}:{_connection.Port} user={_connection.User}");
        _channel = _watch.connect_ras(ref ni, out var res);
        if (_channel < 0)
        {
            Log?.Invoke($"connect_ras returned channel={_channel} err={res.err}");
            ScheduleReconnect();
        }
    }

    private void ScheduleReconnect()
    {
        if (_disposed) return;

        foreach (var tile in _tilesByCamera.Values)
        {
            tile.SetStatus(TileStatus.Disconnected, null);
        }

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        var nextAttemptUtc = DateTime.UtcNow + ReconnectDelay;
        ReconnectScheduled?.Invoke(nextAttemptUtc);

        Task.Delay(ReconnectDelay, token).ContinueWith(t =>
        {
            if (t.IsCanceled || _disposed) return;
            lock (_stateLock)
            {
                if (_disposed) return;
                ConnectInternal();
            }
        }, TaskScheduler.Default);
    }

    // ---- g2watch_listener ----------------------------------------------------

    void g2watch_listener.on_g2watch_connected(int handle, int channel)
    {
        Log?.Invoke($"connected channel={channel}");
        _watch.set_session_timeout(channel, 120_000);

        // Subscribe to all registered cameras + their stream ids in one go.
        var cameras = new g2channel_set();
        var streams = new g2channel_stream_set();
        foreach (var tile in _tilesByCamera.Values)
        {
            cameras.insert(tile.CameraIndex);
            streams.insert(tile.CameraIndex, tile.StreamId);
        }
        _watch.set_camera_list(channel, cameras, force: false);
        _watch.set_camera_stream_set(channel, streams, force: false);
        foreach (var tile in _tilesByCamera.Values)
        {
            _watch.set_stream_id(channel, tile.CameraIndex, tile.StreamId);
        }

        Connected?.Invoke(channel);
    }

    void g2watch_listener.on_g2watch_disconnected(int handle, int channel, G2DISCONNECT_REASON.TYPE reason)
    {
        Log?.Invoke($"disconnected channel={channel} reason={reason}");
        _channel = -1;
        Disconnected?.Invoke(reason);

        // LOGOUT means we initiated the disconnect (Dispose); don't auto-reconnect.
        if (reason != G2DISCONNECT_REASON.TYPE.LOGOUT && !_disposed)
        {
            ScheduleReconnect();
        }
    }

    void g2watch_listener.on_g2watch_receive_frame_data(int handle, int channel, ref G2FRAME frame)
    {
        if (_tilesByCamera.TryGetValue(frame.channel, out var tile))
        {
            // The shared g2decoder is not thread-safe; the tester locks around every
            // decompress call (see screen_pane.cs:542). Frame callbacks for different
            // cameras can arrive on different GDK threads concurrently.
            tile.HandleFrame(_decoder, _decoderLock, ref frame);
        }
    }

    void g2watch_listener.on_g2watch_receive_camera_title_idr(int handle, int channel, int camera, string title) { }
    void g2watch_listener.on_g2watch_receive_audio_data(int handle, int channel, ref G2FRAME frame) { }
    void g2watch_listener.on_g2watch_receive_event(int handle, int channel, ref G2EVENT_INFO ei) { }
    void g2watch_listener.on_g2watch_receive_device_status(int handle, int channel, ref G2DEVICE_STATUS status)
    {
        // status._cameras carries per-camera VIDEOLOSS / NOTCONNECTED flags. We could
        // reflect those into TileStatus here in a future iteration; for now the absence
        // of frames is enough indication.
    }
    void g2watch_listener.on_g2watch_receive_ptz_preset(int handle, int channel, int camera, ref G2LIVE_PTZ_PRESET preset) { }
    void g2watch_listener.on_g2watch_receive_ptz_menu(int handle, int channel, int camera, ref G2LIVE_PTZ_MENU menu) { }
    void g2watch_listener.on_g2watch_receive_text_in(int handle, int channel, ref G2TEXT_IN data) { }
    void g2watch_listener.on_g2watch_receive_network_camera_information(int handle, int channel) { }
    void g2watch_listener.on_g2watch_receive_audio_out_not_available(int handle, int channel) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_color_status(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_COLOR control, ref G2LIVE_COMMAND_CONTROL_COLOR_RANGE range) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_color(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_COLOR control, G2LIVE_COMMAND_RESULT.TYPE result) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_ptz_status(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_PTZ control, ref G2LIVE_COMMAND_CONTROL_PTZ_RANGE range) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_ptz(int handle, int channel, int camera, G2LIVE_COMMAND_RESULT.TYPE result) { }
    void g2watch_listener.on_g2watch_receive_network_alarm_result(int handle, int channel, ref G2LIVE_NETWORK_ALARM_RESULT result) { }
    void g2watch_listener.on_g2watch_receive_elevator_status_info_response(int handle, int channel, uint seq_number) { }
    void g2watch_listener.on_g2watch_receive_instant_recording_start(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result, G2INSTANT_RECORDING_CHANNEL_STATUS[] status) { }
    void g2watch_listener.on_g2watch_receive_instant_recording_stop(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result) { }
    void g2watch_listener.on_g2watch_receive_instant_recording_status(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result, G2INSTANT_RECORDING_CHANNEL_STATUS[] status) { }
    void g2watch_listener.on_g2watch_receive_rds_remote_control(int handle, int channel, ref G2LIVE_RDS_REMOTE_CONTROL_RESPONSE resp) { }
    void g2watch_listener.on_g2watch_receive_rest_api(int handle, int channel, ref G2REST_API_RESPONSE resp) { }
    void g2watch_listener.on_g2watch_receive_gps_data(int handle, int channel, ref G2EVENT gps_data) { }
    void g2watch_listener.on_g2watch_audio_streaming_started(int handle, int channel, int camera) { }
    void g2watch_listener.on_g2watch_audio_streaming_stopped(int handle, int channel, int camera) { }
    void g2watch_listener.on_g2watch_audio_capturing_started(int handle, int channel, int camera) { }
    void g2watch_listener.on_g2watch_audio_capturing_stopped(int handle, int channel, int camera) { }
    void g2watch_listener.on_g2watch_probe_session_profile(int handle, int channel, ref G2PROBE_SESSION_PROFILE probe) { }
    void g2watch_listener.on_g2watch_si_fns_receive_audio_out_open(int handle, int channel, ref G2AUDIO_CODEC_INFO ci, bool available, int num) { }
    void g2watch_listener.on_g2watch_si_fns_receive_audio_codec_info(int handle, int channel, ref G2AUDIO_CODEC_INFO ci, int num) { }
}
