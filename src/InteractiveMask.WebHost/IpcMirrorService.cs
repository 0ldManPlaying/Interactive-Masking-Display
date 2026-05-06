using InteractiveMask.Ipc;
using System.Text.Json;

namespace InteractiveMask.WebHost;

/// <summary>
/// Background service that owns the <see cref="IpcClient"/>: connects to the
/// Display.exe named pipe, decodes incoming envelopes, and feeds the
/// <see cref="StateMirror"/>. Reconnect is handled by the IPC client itself.
/// </summary>
public sealed class IpcMirrorService : BackgroundService
{
    private readonly StateMirror _mirror;
    private readonly IpcCommandSender _commandSender;
    private readonly ILogger<IpcMirrorService> _logger;
    private IpcClient? _client;

    public IpcMirrorService(StateMirror mirror, IpcCommandSender commandSender, ILogger<IpcMirrorService> logger)
    {
        _mirror = mirror;
        _commandSender = commandSender;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new IpcClient();
        _client.Log += msg => _logger.LogInformation("ipc: {Msg}", msg);
        _client.ConnectedChanged += connected =>
        {
            _mirror.SetConnected(connected);
            _logger.LogInformation("ipc connection state: {State}", connected ? "connected" : "disconnected");
        };
        _client.MessageReceived += OnMessage;
        _commandSender.Attach(_client);
        _client.Start();

        // Keep the service alive until the host shuts down.
        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        return tcs.Task;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _client?.Dispose();
        _client = null;
        return base.StopAsync(cancellationToken);
    }

    private void OnMessage(IpcEnvelope env)
    {
        try
        {
            switch (env.Type)
            {
                case IpcMessageType.Hello:
                    var hello = env.Payload.Deserialize<HelloDto>(IpcJson.Options);
                    if (hello is not null) _mirror.ApplyHello(hello);
                    break;

                case IpcMessageType.TileStateChanged:
                    var tile = env.Payload.Deserialize<TileStateDto>(IpcJson.Options);
                    if (tile is not null) _mirror.ApplyTileUpdate(tile);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("failed to decode ipc payload: {Msg}", ex.Message);
        }
    }
}
