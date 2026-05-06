// PoC.GdkConnect — fase 2 proof-of-concept for the InteractiveMask project.
// Goal: verify that the IDIS GDK C# bindings work end-to-end on .NET 9 / x64,
// connect to a single NVR via direct LAN (IP+port+user+pass), receive stream-2
// video frames from one camera, and log basic stats (codec, resolution, fps).
//
// Usage:
//   PoC.GdkConnect --ip 192.168.1.100 --port 8016 --user admin --pass secret \
//                  --camera 0 --stream 1 --duration 30

using GDK;
using InteractiveMask.Gdk;
using System.Diagnostics;

var opts = CliOptions.Parse(args);
if (opts is null) return 1;

Console.WriteLine("InteractiveMask — GDK PoC");
Console.WriteLine($"  GDK DLL    : {GdkProbe.DllName}");
Console.WriteLine($"  Target NVR : {opts.Ip}:{opts.Port} as {opts.User}");
Console.WriteLine($"  Camera     : index {opts.Camera}, stream_id {opts.Stream} (0=main, 1=stream2)");
Console.WriteLine($"  Duration   : {opts.DurationSeconds}s");
Console.WriteLine();

g2main.app_initialize(G2LANGUAGE.ID.ENGLISH);
g2main.set_client_product_info("InteractiveMask-PoC");

// Verbose logging to surface why the connection might fail before on_connected fires.
// Keep a strong reference to the delegate so the GC does not collect it while native
// code holds a function pointer.
G2VERBOSE_CALLBACK verboseCb = (level, msg) => Console.WriteLine($"[gdk:{level}] {msg}");
g2main.verbose_set_level(G2MAIN_VERBOSE.LEVEL.VERBOSE);
g2main.verbose_callback_invoke(verboseCb);

var watch = new g2watch();
var listener = new PocWatchListener(watch, opts);
watch.set_listener(listener);

// startup(connections, alive_check_interval_ms, focus_hwnd).
// tester_watch passes a Forms HWND; for a console app IntPtr.Zero should work
// since the GDK posts events via internal threads, not via the focus window.
watch.startup(connections: 1, alive_check_interval: 60_000, focus: IntPtr.Zero);

var ni = BuildNetworkInfo(opts);
G2CONNECT_RES connectRes;
int channel = watch.connect_ras(ref ni, out connectRes);

if (channel < 0)
{
    Console.Error.WriteLine($"connect_ras failed (channel={channel}). error={connectRes.err}");
    watch.cleanup();
    g2main.app_finalize();
    return 2;
}

listener.Channel = channel;
Console.WriteLine($"connect_ras dispatched on channel {channel}. Waiting for callbacks ...");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.DurationSeconds));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (TaskCanceledException) { /* expected */ }

Console.WriteLine();
Console.WriteLine("Shutting down ...");
watch.disconnect(channel);
await Task.Delay(500); // grace period for disconnect callback
watch.cleanup();
g2main.app_finalize();

listener.PrintSummary();
return 0;

static G2NETWORK_INFO BuildNetworkInfo(CliOptions o)
{
    var ni = new G2NETWORK_INFO();
    ni._address = o.Ip;
    ni._user_id = o.User;
    ni._password = o.Pass;
    // Live video uses WATCH_PORT, not SERVICE_PORT. Setting SERVICE_PORT here causes
    // an immediate disconnect with reason UNKNOWN. See tester_watch/form_watch.cs:67.
    ni.set_port(G2NETWORK_INFO.PORT_TYPE.WATCH_PORT, (ushort)o.Port);
    return ni;
}

internal sealed record CliOptions(
    string Ip,
    int Port,
    string User,
    string Pass,
    int Camera,
    int Stream,
    int DurationSeconds)
{
    public static CliOptions? Parse(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            dict[args[i].TrimStart('-')] = args[i + 1];
        }

        if (!dict.TryGetValue("ip", out var ip) ||
            !dict.TryGetValue("user", out var user) ||
            !dict.TryGetValue("pass", out var pass))
        {
            Console.Error.WriteLine("Required: --ip <addr> --user <name> --pass <secret> [--port 8016] [--camera 0] [--stream 1] [--duration 30]");
            return null;
        }

        return new CliOptions(
            Ip: ip,
            Port: dict.TryGetValue("port", out var p) ? int.Parse(p) : 8016,
            User: user,
            Pass: pass,
            Camera: dict.TryGetValue("camera", out var c) ? int.Parse(c) : 0,
            Stream: dict.TryGetValue("stream", out var s) ? int.Parse(s) : 1,
            DurationSeconds: dict.TryGetValue("duration", out var d) ? int.Parse(d) : 30);
    }
}

internal sealed class PocWatchListener : g2watch_listener
{
    private readonly g2watch _watch;
    private readonly CliOptions _opts;
    private readonly Stopwatch _firstFrame = new();
    private long _frameCount;
    private G2DECODER_CODEC.TYPE _codec = G2DECODER_CODEC.TYPE.UNDEFINED;
    private int _width, _height;

    public int Channel { get; set; } = -1;

    public PocWatchListener(g2watch watch, CliOptions opts)
    {
        _watch = watch;
        _opts = opts;
    }

    public void on_g2watch_connected(int handle, int channel)
    {
        Console.WriteLine($"[connected]   channel={channel}");
        _watch.set_session_timeout(channel, 120_000);

        // Subscribe to ONLY the requested camera with the requested stream_id.
        var cameras = new g2channel_set();
        cameras.insert(_opts.Camera);
        _watch.set_camera_list(channel, cameras, force: false);

        var streams = new g2channel_stream_set();
        streams.insert(_opts.Camera, _opts.Stream);
        _watch.set_camera_stream_set(channel, streams, force: false);
        _watch.set_stream_id(channel, _opts.Camera, _opts.Stream);
    }

    public void on_g2watch_disconnected(int handle, int channel, G2DISCONNECT_REASON.TYPE reason)
    {
        Console.WriteLine($"[disconnect]  channel={channel} reason={reason}");
    }

    public void on_g2watch_receive_frame_data(int handle, int channel, ref G2FRAME frame)
    {
        if (_frameCount == 0)
        {
            _codec = (G2DECODER_CODEC.TYPE)frame._extra._decoder;
            _width = frame._width;
            _height = frame._height;
            _firstFrame.Start();
            Console.WriteLine($"[frame#1]     channel={channel} cam={frame.channel} stream={frame.stream_id} codec={_codec} {_width}x{_height}");
        }
        _frameCount++;

        if (_firstFrame.ElapsedMilliseconds > 0 && _frameCount % 25 == 0)
        {
            double secs = _firstFrame.Elapsed.TotalSeconds;
            double fps = _frameCount / secs;
            Console.WriteLine($"[stats]       frames={_frameCount} elapsed={secs:F1}s avg_fps={fps:F2}");
        }
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"frames received : {_frameCount}");
        Console.WriteLine($"codec           : {_codec}");
        Console.WriteLine($"resolution      : {_width}x{_height}");
        if (_frameCount > 0)
        {
            double secs = _firstFrame.Elapsed.TotalSeconds;
            Console.WriteLine($"avg fps         : {(secs > 0 ? _frameCount / secs : 0):F2}");
        }
    }

    // Required interface members, unused for the PoC.
    public void on_g2watch_receive_audio_data(int handle, int channel, ref G2FRAME frame) { }
    public void on_g2watch_receive_event(int handle, int channel, ref G2EVENT_INFO ei) { }
    public void on_g2watch_receive_device_status(int handle, int channel, ref G2DEVICE_STATUS status) { }
    public void on_g2watch_receive_ptz_preset(int handle, int channel, int camera, ref G2LIVE_PTZ_PRESET preset) { }
    public void on_g2watch_receive_ptz_menu(int handle, int channel, int camera, ref G2LIVE_PTZ_MENU menu) { }
    public void on_g2watch_receive_camera_title_idr(int handle, int channel, int camera, string title) { }
    public void on_g2watch_receive_text_in(int handle, int channel, ref G2TEXT_IN data) { }
    public void on_g2watch_receive_network_camera_information(int handle, int channel) { }
    public void on_g2watch_receive_audio_out_not_available(int handle, int channel) { }
    public void on_g2watch_receive_command_result_control_color_status(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_COLOR control, ref G2LIVE_COMMAND_CONTROL_COLOR_RANGE range) { }
    public void on_g2watch_receive_command_result_control_color(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_COLOR control, G2LIVE_COMMAND_RESULT.TYPE result) { }
    public void on_g2watch_receive_command_result_control_ptz_status(int handle, int channel, int camera, ref G2LIVE_COMMAND_CONTROL_PTZ control, ref G2LIVE_COMMAND_CONTROL_PTZ_RANGE range) { }
    public void on_g2watch_receive_command_result_control_ptz(int handle, int channel, int camera, G2LIVE_COMMAND_RESULT.TYPE result) { }
    public void on_g2watch_receive_network_alarm_result(int handle, int channel, ref G2LIVE_NETWORK_ALARM_RESULT result) { }
    public void on_g2watch_receive_elevator_status_info_response(int handle, int channel, uint seq_number) { }
    public void on_g2watch_receive_instant_recording_start(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result, G2INSTANT_RECORDING_CHANNEL_STATUS[] status) { }
    public void on_g2watch_receive_instant_recording_stop(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result) { }
    public void on_g2watch_receive_instant_recording_status(int handle, int channel, G2INSTANT_RECORDING_RESULT.TYPE result, G2INSTANT_RECORDING_CHANNEL_STATUS[] status) { }
    public void on_g2watch_receive_rds_remote_control(int handle, int channel, ref G2LIVE_RDS_REMOTE_CONTROL_RESPONSE resp) { }
    public void on_g2watch_receive_rest_api(int handle, int channel, ref G2REST_API_RESPONSE resp) { }
    public void on_g2watch_receive_gps_data(int handle, int channel, ref G2EVENT gps_data) { }
    public void on_g2watch_audio_streaming_started(int handle, int channel, int camera) { }
    public void on_g2watch_audio_streaming_stopped(int handle, int channel, int camera) { }
    public void on_g2watch_audio_capturing_started(int handle, int channel, int camera) { }
    public void on_g2watch_audio_capturing_stopped(int handle, int channel, int camera) { }
    public void on_g2watch_probe_session_profile(int handle, int channel, ref G2PROBE_SESSION_PROFILE probe) { }
    public void on_g2watch_si_fns_receive_audio_out_open(int handle, int channel, ref G2AUDIO_CODEC_INFO ci, bool available, int num) { }
    public void on_g2watch_si_fns_receive_audio_codec_info(int handle, int channel, ref G2AUDIO_CODEC_INFO ci, int num) { }
}
