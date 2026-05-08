#nullable enable
using GDK;

namespace InteractiveMask.Gdk;

/// <summary>
/// Lightweight one-shot helper that opens a temporary <c>g2watch</c> session,
/// asks the NVR for its <c>G2DEVICE_STATUS</c>, and returns the configured
/// camera-name strings keyed by camera index.
///
/// Lives independent of the regular <see cref="NvrSession"/> kiosk pipeline:
/// no decoder, no frame subscription. Used by the Setup window so an admin
/// can pull the names the NVR has on file straight into the Cameras tab
/// without needing to start the full live view first.
/// </summary>
public sealed class NvrCameraNameFetcher : g2watch_listener, IDisposable
{
    private readonly g2watch _watch = new();
    private readonly TaskCompletionSource<Dictionary<int, string>> _result = new();
    private int _channel = -1;
    private bool _disposed;

    /// <summary>
    /// Connect to <paramref name="connection"/>, fetch the device-status block
    /// and return the camera-name dictionary (camera-index -> NVR-configured
    /// name, only entries with a non-empty name). Cancels with a TaskCanceled
    /// exception if <paramref name="timeout"/> elapses or the host
    /// <paramref name="cancellationToken"/> is triggered.
    /// </summary>
    public static async Task<Dictionary<int, string>> FetchAsync(
        NvrConnectionInfo connection,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var fetcher = new NvrCameraNameFetcher();
        return await fetcher.RunAsync(connection, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<int, string>> RunAsync(
        NvrConnectionInfo connection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var ni = new G2NETWORK_INFO
        {
            _address = connection.Ip,
            _user_id = connection.User,
            _password = connection.Password,
        };
        ni.set_port(G2NETWORK_INFO.PORT_TYPE.WATCH_PORT, (ushort)connection.Port);
        _watch.set_listener(this);

        _channel = _watch.connect_ras(ref ni, out var res);
        if (_channel < 0)
        {
            throw new InvalidOperationException(
                $"NVR connect failed (err={res.err}). IP={connection.Ip}:{connection.Port} user={connection.User}");
        }

        // Race the device-status callback against a timeout / external cancel.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        using var registration = timeoutCts.Token.Register(() =>
            _result.TrySetException(new TimeoutException(
                $"Camera names not received within {timeout.TotalSeconds:0} s.")));

        try
        {
            return await _result.Task.ConfigureAwait(false);
        }
        finally
        {
            // Always disconnect so we don't leave a phantom session on the NVR.
            try
            {
                if (_channel >= 0) _watch.disconnect(_channel);
            }
            catch
            {
                // Disconnect failures are non-fatal for the fetch result.
            }
        }
    }

    // ---- g2watch_listener ----------------------------------------------------
    //
    // We only care about three callbacks:
    //   - on_connected: trigger the device-status request.
    //   - on_receive_device_status: extract names and complete the task.
    //   - on_disconnected: surface unexpected disconnects as an error.
    // The rest are stubbed out.

    void g2watch_listener.on_g2watch_connected(int handle, int channel)
    {
        try
        {
            _watch.set_session_timeout(channel, 30_000);
            _watch.request_device_status(channel);
        }
        catch (Exception ex)
        {
            _result.TrySetException(ex);
        }
    }

    void g2watch_listener.on_g2watch_disconnected(int handle, int channel, G2DISCONNECT_REASON.TYPE reason)
    {
        // LOGOUT means we initiated; ignore. Anything else before the device
        // status arrived is an error we should surface.
        if (reason == G2DISCONNECT_REASON.TYPE.LOGOUT) return;
        if (!_result.Task.IsCompleted)
        {
            _result.TrySetException(new InvalidOperationException(
                $"NVR disconnected before device status arrived (reason={reason})."));
        }
    }

    void g2watch_listener.on_g2watch_receive_device_status(int handle, int channel, ref G2DEVICE_STATUS status)
    {
        try
        {
            var names = new Dictionary<int, string>();
            if (status._camera_desc is not null)
            {
                for (int i = 0; i < status._camera_desc.Length; i++)
                {
                    var raw = status._camera_desc[i]._string;
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    // GDK strings come back null-padded; trim aggressively.
                    var trimmed = raw.Trim('\0', ' ', '\t', '\r', '\n');
                    if (trimmed.Length == 0) continue;
                    names[i] = trimmed;
                }
            }
            _result.TrySetResult(names);
        }
        catch (Exception ex)
        {
            _result.TrySetException(ex);
        }
    }

    // ---- unused listener members (fully stubbed) -----------------------------
    void g2watch_listener.on_g2watch_receive_frame_data(int handle, int channel, ref G2FRAME frame) { }
    void g2watch_listener.on_g2watch_receive_audio_data(int handle, int channel, ref G2FRAME frame) { }
    void g2watch_listener.on_g2watch_receive_camera_title_idr(int handle, int channel, int camera, string title) { }
    void g2watch_listener.on_g2watch_receive_event(int handle, int channel, ref G2EVENT_INFO ei) { }
    void g2watch_listener.on_g2watch_receive_ptz_preset(int handle, int channel, int camera, ref G2LIVE_PTZ_PRESET preset) { }
    void g2watch_listener.on_g2watch_receive_ptz_menu(int handle, int channel, int camera, ref G2LIVE_PTZ_MENU menu) { }
    void g2watch_listener.on_g2watch_receive_text_in(int handle, int channel, ref G2TEXT_IN data) { }
    void g2watch_listener.on_g2watch_receive_network_camera_information(int handle, int channel) { }
    void g2watch_listener.on_g2watch_receive_audio_out_not_available(int handle, int channel) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_color_status(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_COLOR control, ref G2LIVE_COMMAND_CONTROL_COLOR_RANGE range) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_color(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_COLOR control, G2LIVE_COMMAND_RESULT.TYPE result) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_ptz_status(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_PTZ control, ref G2LIVE_COMMAND_CONTROL_PTZ_RANGE range) { }
    void g2watch_listener.on_g2watch_receive_command_result_control_ptz(int handle, int channel, int camera, G2LIVE_COMMAND_RESULT.TYPE result) { }
    void g2watch_listener.on_g2watch_receive_gps_data(int handle, int channel, ref G2EVENT data) { }
    void g2watch_listener.on_g2watch_audio_streaming_started(int handle, int channel, int audioChannel) { }
    void g2watch_listener.on_g2watch_audio_streaming_stopped(int handle, int channel, int audioChannel) { }
    void g2watch_listener.on_g2watch_audio_capturing_started(int handle, int channel, int audioChannel) { }
    void g2watch_listener.on_g2watch_audio_capturing_stopped(int handle, int channel, int audioChannel) { }
    void g2watch_listener.on_g2watch_probe_session_profile(int handle, int channel, ref G2PROBE_SESSION_PROFILE profile) { }
    void g2watch_listener.on_g2watch_si_fns_receive_audio_out_open(int handle, int channel, ref G2AUDIO_CODEC_INFO codec, bool result, int errorCode) { }
    void g2watch_listener.on_g2watch_si_fns_receive_audio_codec_info(int handle, int channel, ref G2AUDIO_CODEC_INFO codec, int result) { }
    void g2watch_listener.on_g2watch_receive_network_alarm_result(int handle, int channel, ref G2LIVE_NETWORK_ALARM_RESULT result) { }
    void g2watch_listener.on_g2watch_receive_elevator_status_info_response(int handle, int channel, uint status) { }
    void g2watch_listener.on_g2watch_receive_instant_recording_start(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result, G2INSTANT_RECORDING_CHANNEL_STATUS[] statuses) { }
    void g2watch_listener.on_g2watch_receive_instant_recording_stop(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result) { }
    void g2watch_listener.on_g2watch_receive_instant_recording_status(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result, G2INSTANT_RECORDING_CHANNEL_STATUS[] statuses) { }
    void g2watch_listener.on_g2watch_receive_rds_remote_control(int handle, int channel, ref G2LIVE_RDS_REMOTE_CONTROL_RESPONSE response) { }
    void g2watch_listener.on_g2watch_receive_rest_api(int handle, int channel, ref G2REST_API_RESPONSE response) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _watch.set_listener(null); } catch { }
    }
}
