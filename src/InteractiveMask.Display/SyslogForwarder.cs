using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace InteractiveMask.Display;

/// <summary>
/// Sink for forwarding <see cref="AuditLog"/> events to an external SIEM.
/// Implementations must be non-blocking on the audit-write path; slow networks
/// or unreachable servers MUST NOT delay a privacy-related action.
/// </summary>
public interface IAuditForwarder : IDisposable
{
    void Forward(AuditEvent ev);
}

/// <summary>
/// Real-time RFC 5424 syslog forwarder. Events go onto a bounded background
/// queue and are drained over UDP (fire-and-forget) or TCP (RFC 6587 octet-
/// counting framing, persistent connection with reconnect/backoff).
///
/// PEN 32473 in the structured-data SD-ID is the IANA-reserved-for-documentation
/// enterprise number; replace with a registered PEN once the customer has one.
/// </summary>
public sealed class SyslogForwarder : IAuditForwarder
{
    private const int QueueCapacity = 1024;
    private static readonly TimeSpan TcpRetryBackoff = TimeSpan.FromSeconds(15);

    private readonly AuditForwardSettings _settings;
    private readonly Channel<AuditEvent> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;
    private readonly string _hostname;

    public SyslogForwarder(AuditForwardSettings settings)
    {
        _settings = settings;
        _queue = Channel.CreateBounded<AuditEvent>(new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _hostname = SanitizeAscii(Environment.MachineName, 255);
        _drainTask = Task.Run(DrainLoopAsync);
    }

    public void Forward(AuditEvent ev)
    {
        if (!_settings.SyslogEnabled || string.IsNullOrWhiteSpace(_settings.SyslogHost)) return;
        _queue.Writer.TryWrite(ev);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();
        try { _drainTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* shutdown best-effort */ }
        _cts.Dispose();
    }

    /// <summary>
    /// Send a single test event synchronously and report success/failure.
    /// Used by the Setup window's "Test" button so the admin gets immediate
    /// feedback that host/port/protocol are correct.
    /// </summary>
    public static async Task<(bool ok, string? error)> SendTestAsync(AuditForwardSettings s, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s.SyslogHost)) return (false, "host empty");
        var ev = new AuditEvent(
            Timestamp: DateTimeOffset.Now,
            Type: AuditEventType.AppStarted,
            Slot: null, CameraIndex: null, Label: null,
            Source: "syslog-test",
            Detail: "InteractiveMask syslog test message");
        var msg = FormatRfc5424(ev, s, Environment.MachineName);
        try
        {
            if (string.Equals(s.SyslogProtocol, "tcp", StringComparison.OrdinalIgnoreCase))
            {
                using var tcp = new TcpClient();
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                await tcp.ConnectAsync(s.SyslogHost, s.SyslogPort, connectCts.Token);
                await using var stream = tcp.GetStream();
                var bytes = Encoding.UTF8.GetBytes(msg);
                var prefix = Encoding.ASCII.GetBytes($"{bytes.Length} ");
                await stream.WriteAsync(prefix, ct);
                await stream.WriteAsync(bytes, ct);
                await stream.FlushAsync(ct);
            }
            else
            {
                using var udp = new UdpClient();
                var bytes = Encoding.UTF8.GetBytes(msg);
                await udp.SendAsync(bytes, bytes.Length, s.SyslogHost, s.SyslogPort);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task DrainLoopAsync()
    {
        bool isTcp = string.Equals(_settings.SyslogProtocol, "tcp", StringComparison.OrdinalIgnoreCase);
        UdpClient? udp = null;
        TcpClient? tcp = null;
        NetworkStream? tcpStream = null;
        DateTime nextRetryUtc = DateTime.MinValue;

        try
        {
            await foreach (var ev in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                var msg = FormatRfc5424(ev, _settings, _hostname);
                try
                {
                    if (isTcp)
                    {
                        if (tcp is null || !tcp.Connected)
                        {
                            // Hold off after a failure so we don't spin reconnecting
                            // when the SIEM is down. Drop the event in that window;
                            // the queue still buffers up to QueueCapacity events.
                            if (DateTime.UtcNow < nextRetryUtc) continue;
                            tcp?.Dispose();
                            tcp = new TcpClient();
                            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                            connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                            await tcp.ConnectAsync(_settings.SyslogHost, _settings.SyslogPort, connectCts.Token);
                            tcpStream = tcp.GetStream();
                        }
                        var bytes = Encoding.UTF8.GetBytes(msg);
                        var prefix = Encoding.ASCII.GetBytes($"{bytes.Length} ");
                        await tcpStream!.WriteAsync(prefix, _cts.Token);
                        await tcpStream.WriteAsync(bytes, _cts.Token);
                        await tcpStream.FlushAsync(_cts.Token);
                    }
                    else
                    {
                        udp ??= new UdpClient();
                        var bytes = Encoding.UTF8.GetBytes(msg);
                        await udp.SendAsync(bytes, bytes.Length, _settings.SyslogHost, _settings.SyslogPort);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    nextRetryUtc = DateTime.UtcNow + TcpRetryBackoff;
                    try { tcpStream?.Dispose(); } catch { }
                    tcp?.Dispose();
                    tcp = null;
                    tcpStream = null;
                }
            }
        }
        catch (OperationCanceledException) { /* clean shutdown */ }
        finally
        {
            try { tcpStream?.Dispose(); } catch { }
            tcp?.Dispose();
            udp?.Dispose();
        }
    }

    private static string FormatRfc5424(AuditEvent ev, AuditForwardSettings s, string hostname)
    {
        int severity = SeverityFor(ev.Type);
        int pri = (Math.Clamp(s.SyslogFacility, 0, 23) * 8) + severity;
        var ts = ev.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var appName = SanitizeAscii(string.IsNullOrEmpty(s.SyslogAppName) ? "InteractiveMask" : s.SyslogAppName, 48);
        var sd = BuildStructuredData(ev);

        var msg = ev.Type.ToString();
        if (!string.IsNullOrEmpty(ev.Label)) msg += $" label=\"{Escape(ev.Label!)}\"";
        if (!string.IsNullOrEmpty(ev.Source)) msg += $" source=\"{Escape(ev.Source!)}\"";
        if (!string.IsNullOrEmpty(ev.Detail)) msg += $" detail=\"{Escape(ev.Detail!)}\"";

        // RFC 5424: <PRI>1 TIMESTAMP HOSTNAME APP-NAME PROCID MSGID STRUCTURED-DATA MSG
        return $"<{pri}>1 {ts} {hostname} {appName} - {ev.Type} {sd} {msg}\n";
    }

    private static string BuildStructuredData(AuditEvent ev)
    {
        var sb = new StringBuilder("[interactivemask@32473");
        if (ev.Slot is { } slot)            sb.Append($" slot=\"{slot}\"");
        if (ev.CameraIndex is { } camIdx)   sb.Append($" cameraIndex=\"{camIdx}\"");
        if (!string.IsNullOrEmpty(ev.Source)) sb.Append($" source=\"{Escape(ev.Source!)}\"");
        sb.Append(']');
        return sb.ToString();
    }

    private static int SeverityFor(AuditEventType t) => t switch
    {
        AuditEventType.PinFail                                            => 4, // Warning
        AuditEventType.PinLockout                                         => 3, // Error
        AuditEventType.MaskAutoReset or AuditEventType.NvrDisconnected    => 5, // Notice
        _                                                                 => 6, // Informational
    };

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("]", "\\]").Replace("\n", " ").Replace("\r", " ");

    private static string SanitizeAscii(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "-";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(c is >= (char)33 and <= (char)126 ? c : '_');
            if (sb.Length >= max) break;
        }
        return sb.Length == 0 ? "-" : sb.ToString();
    }
}
