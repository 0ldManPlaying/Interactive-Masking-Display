using InteractiveMask.Ipc;
using System.Collections.Concurrent;
using System.Text.Json;

namespace InteractiveMask.WebHost;

/// <summary>
/// Thin layer over <see cref="IpcClient"/> that handles request/response correlation:
/// callers issue a <see cref="ToggleRequestDto"/> and await the matching
/// <see cref="ToggleResponseDto"/> by request id. Wired into the same singleton
/// IPC client owned by <see cref="IpcMirrorService"/>.
/// </summary>
public sealed class IpcCommandSender
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ToggleResponseDto>> _pending = new();
    private IpcClient? _client;

    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Bind the sender to the IPC client. Call once at startup.</summary>
    public void Attach(IpcClient client)
    {
        _client = client;
        client.MessageReceived += OnMessage;
    }

    public async Task<ToggleResponseDto> ToggleAsync(int slot, string? pin, string? source, CancellationToken token = default)
    {
        var client = _client ?? throw new InvalidOperationException("IPC client not attached");
        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ToggleResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;

        try
        {
            var req = new ToggleRequestDto(requestId, slot, pin, source);
            var envelope = new IpcEnvelope
            {
                Type = IpcMessageType.ToggleRequest,
                Payload = JsonSerializer.SerializeToElement(req, IpcJson.Options),
            };

            var sent = await client.SendAsync(envelope, token);
            if (!sent)
            {
                _pending.TryRemove(requestId, out _);
                throw new InvalidOperationException("ipc not connected");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(DefaultTimeout);
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task;
            }
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    private void OnMessage(IpcEnvelope env)
    {
        if (env.Type != IpcMessageType.ToggleResponse) return;
        ToggleResponseDto? resp;
        try { resp = env.Payload.Deserialize<ToggleResponseDto>(IpcJson.Options); }
        catch { return; }
        if (resp is null) return;

        if (_pending.TryRemove(resp.RequestId, out var tcs))
        {
            tcs.TrySetResult(resp);
        }
    }
}
