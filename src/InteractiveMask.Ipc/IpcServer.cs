using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace InteractiveMask.Ipc;

/// <summary>
/// Per-client session abstraction so request handlers can reply to the specific
/// caller without the broadcast going to every other connected client.
/// </summary>
public interface IIpcSession
{
    Guid Id { get; }
    void Send(IpcEnvelope envelope);
}

/// <summary>
/// Named-pipe server. Listens on <c>\\.\pipe\InteractiveMask</c>, accepts multiple
/// clients, broadcasts state envelopes to all of them, and routes inbound
/// envelopes to a single optional <see cref="RequestHandler"/>.
///
/// Wire format: each envelope is serialised to UTF-8 JSON and terminated by a
/// single LF. Length-delimited binary would be more robust against partial
/// reads, but JSON-line keeps the on-pipe traffic trivially debuggable while we
/// flesh out the protocol.
/// </summary>
public sealed class IpcServer : IDisposable
{
    public const string DefaultPipeName = "InteractiveMask";

    private readonly string _pipeName;
    private readonly ConcurrentDictionary<Guid, ClientChannel> _clients = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private bool _disposed;

    /// <summary>
    /// Invoked once per newly-connected client; must return the initial Hello
    /// envelope sent before any subsequent broadcast.
    /// </summary>
    public Func<IpcEnvelope>? HelloFactory { get; init; }

    /// <summary>
    /// Invoked for every inbound envelope read from a client. Implementers
    /// dispatch on <see cref="IpcEnvelope.Type"/> and may reply via <c>session.Send</c>.
    /// </summary>
    public Action<IpcEnvelope, IIpcSession>? RequestHandler { get; init; }

    public IpcServer(string pipeName = DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    public void Start()
    {
        if (_acceptLoop is not null) return;
        _acceptLoop = Task.Run(() => AcceptLoop(_cts.Token));
    }

    /// <summary>Send an envelope to every currently-connected client.</summary>
    public void Broadcast(IpcEnvelope envelope)
    {
        var payload = SerializeWithNewline(envelope);
        foreach (var client in _clients.Values)
        {
            client.Enqueue(payload);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        foreach (var c in _clients.Values) c.Dispose();
        _clients.Clear();
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(
                    pipeName: _pipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);
            }
            catch
            {
                try { await Task.Delay(500, token); } catch { return; }
                continue;
            }

            try
            {
                await server.WaitForConnectionAsync(token);
            }
            catch
            {
                server.Dispose();
                continue;
            }

            var channel = new ClientChannel(server, this);
            _clients[channel.Id] = channel;

            if (HelloFactory is not null)
            {
                channel.Enqueue(SerializeWithNewline(HelloFactory()));
            }

            _ = channel.RunAsync(token).ContinueWith(t =>
            {
                _clients.TryRemove(channel.Id, out ClientChannel? _);
                channel.Dispose();
            }, TaskScheduler.Default);
        }
    }

    private static byte[] SerializeWithNewline(IpcEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        return Encoding.UTF8.GetBytes(json + "\n");
    }

    internal void DispatchInbound(IpcEnvelope envelope, IIpcSession session)
    {
        try
        {
            RequestHandler?.Invoke(envelope, session);
        }
        catch
        {
            // The server must never propagate handler exceptions out to the
            // accept loop; the offending client just gets no reply.
        }
    }

    /// <summary>Per-client outbound queue, writer task, and reader task.</summary>
    private sealed class ClientChannel : IIpcSession, IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        private readonly NamedPipeServerStream _pipe;
        private readonly IpcServer _server;
        private readonly BlockingCollection<byte[]> _queue = new(boundedCapacity: 256);

        public ClientChannel(NamedPipeServerStream pipe, IpcServer server)
        {
            _pipe = pipe;
            _server = server;
        }

        public void Send(IpcEnvelope envelope) => Enqueue(SerializeWithNewline(envelope));

        public void Enqueue(byte[] payload)
        {
            // Drop the message if the queue is full. Live state changes are
            // produced faster than a slow client can consume them, and we must
            // not back-pressure the producer (the live video pipeline).
            _queue.TryAdd(payload);
        }

        public async Task RunAsync(CancellationToken token)
        {
            // Reader and writer share the same pipe but never overlap because
            // each direction runs in its own task on top of an InOut pipe.
            var writer = Task.Run(() => WriterLoop(token), token);
            var reader = Task.Run(() => ReaderLoop(token), token);
            await Task.WhenAny(writer, reader);
        }

        private async Task WriterLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _pipe.IsConnected)
                {
                    if (!_queue.TryTake(out var payload, 200, token)) continue;
                    await _pipe.WriteAsync(payload, 0, payload.Length, token);
                    await _pipe.FlushAsync(token);
                }
            }
            catch
            {
                // Disconnect / cancellation is the normal exit path.
            }
        }

        private async Task ReaderLoop(CancellationToken token)
        {
            try
            {
                using var reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
                while (!token.IsCancellationRequested && _pipe.IsConnected)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (line is null) return; // pipe closed
                    if (line.Length == 0) continue;

                    IpcEnvelope? env;
                    try
                    {
                        env = JsonSerializer.Deserialize<IpcEnvelope>(line, IpcJson.Options);
                    }
                    catch
                    {
                        continue;
                    }
                    if (env is not null) _server.DispatchInbound(env, this);
                }
            }
            catch
            {
                // Disconnect / cancellation is the normal exit path.
            }
        }

        public void Dispose()
        {
            try { _pipe.Disconnect(); } catch { }
            _pipe.Dispose();
            _queue.CompleteAdding();
            _queue.Dispose();
        }
    }
}
