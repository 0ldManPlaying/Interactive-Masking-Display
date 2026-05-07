using InteractiveMask.Ipc;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace InteractiveMask.Display;

/// <summary>
/// Bridges the WPF <see cref="DisplayViewModel"/> + <see cref="MaskController"/>
/// to the <see cref="IpcServer"/>: pushes state-change broadcasts to remote
/// clients and dispatches inbound toggle commands to the controller on the UI
/// thread.
/// </summary>
public sealed class IpcStateBroadcaster : IDisposable
{
    private static readonly HashSet<string> WatchedProperties = new()
    {
        nameof(TileViewModel.IsMasked),
        nameof(TileViewModel.Status),
        nameof(TileViewModel.IsTimerWarning),
        nameof(TileViewModel.CountdownText),
        nameof(TileViewModel.HasCamera),
        nameof(TileViewModel.Label),
    };

    private readonly DisplayViewModel _viewModel;
    private readonly IpcServer _server;
    private readonly Dictionary<int, (int NvrId, int CameraIndex)> _bindingsBySlot;
    private readonly MaskController _maskController;
    private readonly BlurPinService _pinService;
    private readonly Dispatcher _dispatcher;

    public IpcStateBroadcaster(
        DisplayViewModel viewModel,
        Dictionary<int, (int NvrId, int CameraIndex)> bindingsBySlot,
        MaskController maskController,
        BlurPinService pinService,
        Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _bindingsBySlot = bindingsBySlot;
        _maskController = maskController;
        _pinService = pinService;
        _dispatcher = dispatcher;
        _server = new IpcServer
        {
            HelloFactory = BuildHelloEnvelope,
            RequestHandler = OnInboundRequest,
        };

        foreach (var tile in _viewModel.Tiles)
        {
            tile.PropertyChanged += OnTilePropertyChanged;
        }
    }

    public void Start() => _server.Start();

    public void Dispose()
    {
        foreach (var tile in _viewModel.Tiles)
        {
            tile.PropertyChanged -= OnTilePropertyChanged;
        }
        _server.Dispose();
    }

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TileViewModel tile) return;
        if (e.PropertyName is null || !WatchedProperties.Contains(e.PropertyName)) return;

        var dto = ToDto(tile);
        var envelope = new IpcEnvelope
        {
            Type = IpcMessageType.TileStateChanged,
            Payload = JsonSerializer.SerializeToElement(dto, IpcJson.Options),
        };
        _server.Broadcast(envelope);
    }

    private IpcEnvelope BuildHelloEnvelope()
    {
        var hello = new HelloDto(
            Rows: _viewModel.Rows,
            Columns: _viewModel.Columns,
            Tiles: _viewModel.Tiles.Select(ToDto).ToList());
        return new IpcEnvelope
        {
            Type = IpcMessageType.Hello,
            Payload = JsonSerializer.SerializeToElement(hello, IpcJson.Options),
        };
    }

    private void OnInboundRequest(IpcEnvelope envelope, IIpcSession session)
    {
        if (envelope.Type == IpcMessageType.SnapshotRequest)
        {
            HandleSnapshotRequest(envelope, session);
            return;
        }
        if (envelope.Type != IpcMessageType.ToggleRequest) return;

        ToggleRequestDto? req;
        try { req = envelope.Payload.Deserialize<ToggleRequestDto>(IpcJson.Options); }
        catch { return; }
        if (req is null) return;

        // Mask state lives on the UI thread (TileViewModel + dispatcher-bound
        // ObservableCollection). Marshal there before mutating, then send the
        // result back via the same client session.
        _dispatcher.BeginInvoke(() =>
        {
            ToggleResult result;
            int? lockoutSeconds = null;

            if (req.Slot < 0 || req.Slot >= _viewModel.Tiles.Count)
            {
                result = ToggleResult.InvalidSlot;
            }
            else
            {
                var tile = _viewModel.Tiles[req.Slot];
                result = _maskController.HandleRemoteToggle(tile, req.Pin, req.Source, req.PreAuthenticated);
            }

            if (result == ToggleResult.LockedOut)
            {
                lockoutSeconds = (int)Math.Ceiling(_pinService.LockoutRemaining.TotalSeconds);
            }

            var response = new ToggleResponseDto(req.RequestId, result, lockoutSeconds);
            session.Send(new IpcEnvelope
            {
                Type = IpcMessageType.ToggleResponse,
                Payload = JsonSerializer.SerializeToElement(response, IpcJson.Options),
            });
        });
    }

    private void HandleSnapshotRequest(IpcEnvelope envelope, IIpcSession session)
    {
        SnapshotRequestDto? req;
        try { req = envelope.Payload.Deserialize<SnapshotRequestDto>(IpcJson.Options); }
        catch { return; }
        if (req is null) return;

        _dispatcher.BeginInvoke(() =>
        {
            var status = SnapshotStatus.Empty;
            string? jpeg = null;

            if (req.Slot >= 0 && req.Slot < _viewModel.Tiles.Count)
            {
                var tile = _viewModel.Tiles[req.Slot];
                if (!tile.HasCamera)               status = SnapshotStatus.Empty;
                else if (tile.IsMasked)            status = SnapshotStatus.Masked;
                else if (tile.Bitmap is null)      status = SnapshotStatus.NoFrame;
                else
                {
                    try
                    {
                        jpeg = EncodeJpegBase64(tile.Bitmap);
                        status = SnapshotStatus.Ok;
                    }
                    catch
                    {
                        status = SnapshotStatus.NoFrame;
                    }
                }
            }

            session.Send(new IpcEnvelope
            {
                Type = IpcMessageType.SnapshotResponse,
                Payload = JsonSerializer.SerializeToElement(
                    new SnapshotResponseDto(req.RequestId, req.Slot, status, jpeg),
                    IpcJson.Options),
            });
        });
    }

    /// <summary>
    /// Encode the current frame as a low-quality JPEG. We intentionally cap
    /// quality at 60 — these snapshots are diagnostic thumbnails for the web
    /// UI, not archival images, and a smaller payload keeps the IPC envelope
    /// snappy. Runs on the UI thread so it serialises naturally with frame
    /// writes (RenderFrame also runs there).
    /// </summary>
    private static string EncodeJpegBase64(BitmapSource source)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 60 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private TileStateDto ToDto(TileViewModel tile)
    {
        int cameraIndex = _bindingsBySlot.TryGetValue(tile.SlotIndex, out var identity)
            ? identity.CameraIndex
            : -1;
        return new TileStateDto(
            Slot: tile.SlotIndex,
            CameraIndex: cameraIndex,
            Label: tile.Label,
            Status: tile.Status.ToString(),
            IsMasked: tile.IsMasked,
            IsTimerWarning: tile.IsTimerWarning,
            CountdownText: tile.CountdownText);
    }
}
