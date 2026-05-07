using System.IO.Pipes;
using System.Text.Json;

namespace InteractiveMask.Ipc;

/// <summary>
/// Named-pipe client. Connects to <c>\\.\pipe\InteractiveMask</c>, reads
/// JSON-line envelopes, and dispatches them via the <see cref="MessageReceived"/>
/// event. Reconnects automatically when the connection drops.
/// </summary>
public sealed class IpcClient : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runLoop;
    private bool _disposed;

    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(2);

    public event Action<IpcEnvelope>? MessageReceived;
    public event Action<string>? Log;
    public event Action<bool>? ConnectedChanged;

    public IpcClient(string pipeName = IpcServer.DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    private NamedPipeClientStream? _activePipe;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public void Start()
    {
        if (_runLoop is not null) return;
        _runLoop = Task.Run(() => RunLoop(_cts.Token));
    }

    /// <summary>
    /// Send an envelope to the connected server. Returns false when no pipe is
    /// currently connected; callers can decide whether to retry.
    /// </summary>
    public async Task<bool> SendAsync(IpcEnvelope envelope, CancellationToken token = default)
    {
        var pipe = _activePipe;
        if (pipe is null || !pipe.IsConnected) return false;

        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json + "\n");

        await _writeLock.WaitAsync(token);
        try
        {
            await pipe.WriteAsync(bytes, 0, bytes.Length, token);
            await pipe.FlushAsync(token);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _writeLock.Dispose();
        _cts.Dispose();
    }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            try
            {
                Log?.Invoke($"connecting to pipe {_pipeName} ...");
                await pipe.ConnectAsync(timeout: 5_000, token);
                _activePipe = pipe;
                ConnectedChanged?.Invoke(true);
                Log?.Invoke("ipc connected");
                await ReadLoop(pipe, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"ipc error: {ex.Message}");
            }
            finally
            {
                _activePipe = null;
                ConnectedChanged?.Invoke(false);
            }

            try { await Task.Delay(ReconnectDelay, token); } catch { return; }
        }
    }

    private async Task ReadLoop(NamedPipeClientStream pipe, CancellationToken token)
    {
        using var reader = new StreamReader(pipe, leaveOpen: true);
        while (!token.IsCancellationRequested && pipe.IsConnected)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null) break; // pipe closed
            if (line.Length == 0) continue;

            IpcEnvelope? env;
            try
            {
                env = JsonSerializer.Deserialize<IpcEnvelope>(line, IpcJson.Options);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"ipc parse error: {ex.Message}");
                continue;
            }

            if (env is not null)
            {
                MessageReceived?.Invoke(env);
            }
        }
    }
}
