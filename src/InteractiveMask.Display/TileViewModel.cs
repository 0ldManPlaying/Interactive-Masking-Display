using InteractiveMask.Gdk;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace InteractiveMask.Display;

public sealed class TileViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly PropertyChangedEventHandler _onLanguageChanged;
    private CameraTile? _camera;
    private bool _disposed;

    private WriteableBitmap? _bitmap;
    private string _label = "";
    private string _statusText = "";
    private Brush _statusBrush = Brushes.Gray;
    private bool _hasCamera;
    private bool _isMasked;
    private TileStatus _status = TileStatus.Empty;
    private DateTime? _maskedAtUtc;
    private int _autoUnmaskMinutes;
    private bool _isTimerWarning;
    private string _countdownText = "";

    /// <summary>
    /// Gaussian blur radius applied to the camera image. 0 = no blur, ~80 = heavy
    /// blur such that no silhouette / motion / text is recognizable while colour
    /// blobs remain visible (the chosen privacy style).
    /// </summary>
    public const double MaskedBlurRadius = 80.0;

    public int SlotIndex { get; }

    public bool HasCamera
    {
        get => _hasCamera;
        private set => Set(ref _hasCamera, value);
    }

    public string Label
    {
        get => _label;
        private set => Set(ref _label, value);
    }

    public WriteableBitmap? Bitmap
    {
        get => _bitmap;
        private set => Set(ref _bitmap, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => Set(ref _statusBrush, value);
    }

    /// <summary>Machine-readable status mirroring the underlying <see cref="CameraTile"/>.</summary>
    public TileStatus Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>
    /// Whether the privacy mask is active. Public-set so <see cref="MaskController"/>
    /// can drive it after running the PIN flow; the view-model itself is intentionally
    /// PIN-agnostic.
    /// </summary>
    public bool IsMasked
    {
        get => _isMasked;
        set
        {
            if (Set(ref _isMasked, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlurRadius)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaskOverlayText)));
            }
        }
    }

    public double BlurRadius => _isMasked ? MaskedBlurRadius : 0.0;

    public string MaskOverlayText => $"{Strings.Instance.Current.PrivacyActive} — {Label}";

    /// <summary>True in the final <c>WarningMinutes</c> minutes before auto-unmask.</summary>
    public bool IsTimerWarning
    {
        get => _isTimerWarning;
        private set => Set(ref _isTimerWarning, value);
    }

    /// <summary>"auto-uit 03:42" or "" when no timer is active.</summary>
    public string CountdownText
    {
        get => _countdownText;
        private set => Set(ref _countdownText, value);
    }

    /// <summary>UTC timestamp when the current mask was applied; null when not masked.</summary>
    public DateTime? MaskedAtUtc => _maskedAtUtc;

    /// <summary>Minutes after MaskedAtUtc at which the auto-unmask fires; 0 = disabled.</summary>
    public int AutoUnmaskMinutes => _autoUnmaskMinutes;

    /// <summary>Apply or remove the privacy mask, optionally arming the auto-unmask timer.</summary>
    public void SetMasked(bool masked, int autoUnmaskMinutes = 0, DateTime? maskedAtUtc = null)
    {
        IsMasked = masked;
        if (masked)
        {
            _autoUnmaskMinutes = Math.Max(0, autoUnmaskMinutes);
            _maskedAtUtc = _autoUnmaskMinutes > 0 ? (maskedAtUtc ?? DateTime.UtcNow) : null;
        }
        else
        {
            _autoUnmaskMinutes = 0;
            _maskedAtUtc = null;
            IsTimerWarning = false;
            CountdownText = "";
        }
    }

    /// <summary>
    /// Called once per second by the global ticker. Updates <see cref="CountdownText"/>
    /// and <see cref="IsTimerWarning"/>; returns true when the timer has expired and
    /// the tile should be auto-unmasked (caller must run the unmask logic).
    /// </summary>
    public bool TickAutoUnmask(int warningMinutes)
    {
        if (!IsMasked || _maskedAtUtc is null || _autoUnmaskMinutes <= 0)
        {
            if (IsTimerWarning) IsTimerWarning = false;
            if (CountdownText.Length > 0) CountdownText = "";
            return false;
        }

        var elapsed = DateTime.UtcNow - _maskedAtUtc.Value;
        var total = TimeSpan.FromMinutes(_autoUnmaskMinutes);
        var remaining = total - elapsed;

        if (remaining <= TimeSpan.Zero) return true;

        IsTimerWarning = remaining <= TimeSpan.FromMinutes(Math.Max(0, warningMinutes));
        CountdownText = $"{Strings.Instance.Current.AutoOff} {remaining:mm\\:ss}";
        return false;
    }

    public TileViewModel(int slotIndex, Dispatcher dispatcher)
    {
        SlotIndex = slotIndex;
        _dispatcher = dispatcher;
        // Re-emit text-bearing properties when the language changes so live taal-switch
        // affects already-bound overlay texts and status labels.
        // Stored as a field so Dispose can unsubscribe — without that, every
        // grid-resize creates new TileViewModels that hold a static-event back
        // reference and never get GC'd.
        _onLanguageChanged = (_, _) => RefreshLanguageStrings();
        Strings.Instance.PropertyChanged += _onLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Strings.Instance.PropertyChanged -= _onLanguageChanged;
        Detach();
    }

    private void RefreshLanguageStrings()
    {
        // MaskOverlayText, CountdownText and StatusText all interpolate Strings.Current.
        OnPropertyChanged(nameof(MaskOverlayText));
        OnPropertyChanged(nameof(CountdownText));
        // Re-derive StatusText from the current TileStatus.
        if (_camera is not null) ApplyStatus(_camera.Status, _camera.LastError);
    }

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Update the displayed label without re-attaching the camera. Used by the live-apply path.</summary>
    public void UpdateLabel(string newLabel)
    {
        Label = string.IsNullOrWhiteSpace(newLabel) ? Label : newLabel;
        OnPropertyChanged(nameof(MaskOverlayText));
    }

    /// <summary>
    /// Surface a friendly disconnect reason on the status bar of the tile when the
    /// underlying NVR session drops. Cleared automatically on the next status update.
    /// </summary>
    public void SetDisconnectDetail(string detail)
    {
        if (_camera is null || _camera.Status == TileStatus.Live) return;
        StatusText = detail;
    }

    /// <summary>Bind a camera to this tile. Subscribes to its frame and status events.</summary>
    public void Attach(CameraTile camera)
    {
        Detach();

        _camera = camera;
        Label = string.IsNullOrWhiteSpace(camera.Label) ? $"Camera {camera.CameraIndex}" : camera.Label;
        HasCamera = true;
        ApplyStatus(camera.Status, camera.LastError);

        camera.FrameDecoded += OnFrameDecoded;
        camera.StatusChanged += OnStatusChanged;
    }

    public void Detach()
    {
        if (_camera is null) return;
        _camera.FrameDecoded -= OnFrameDecoded;
        _camera.StatusChanged -= OnStatusChanged;
        _camera = null;
        HasCamera = false;
        Bitmap = null;
        Label = "";
        StatusText = "";
        StatusBrush = Brushes.Gray;
    }

    private void OnFrameDecoded(DecodedFrame frame)
    {
        // Called on GDK thread. Marshal the bitmap update to the UI thread.
        // BeginInvoke (not Invoke) so the GDK callback can return immediately and the
        // next frame's decode pipeline isn't blocked on UI work.
        _dispatcher.BeginInvoke(() => RenderFrame(frame), DispatcherPriority.Render);
    }

    private void RenderFrame(DecodedFrame frame)
    {
        if (Bitmap is null || Bitmap.PixelWidth != frame.Width || Bitmap.PixelHeight != frame.Height)
        {
            Bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
        }
        var rect = new Int32Rect(0, 0, frame.Width, frame.Height);
        Bitmap.WritePixels(rect, frame.Buffer, frame.Stride * frame.Height, frame.Stride);
    }

    private void OnStatusChanged(TileStatus status, string? detail)
    {
        _dispatcher.BeginInvoke(() => ApplyStatus(status, detail));
    }

    private void ApplyStatus(TileStatus status, string? detail)
    {
        Status = status;
        var s = Strings.Instance.Current;
        StatusText = status switch
        {
            TileStatus.Live         => "",
            TileStatus.Pending      => s.StatusConnecting,
            TileStatus.Disconnected => s.StatusDisconnected,
            TileStatus.VideoLoss    => s.StatusVideoLoss,
            TileStatus.Error        => s.ErrorPrefix + detail,
            _                       => "",
        };
        StatusBrush = status switch
        {
            TileStatus.Live => Brushes.LimeGreen,
            TileStatus.Pending => Brushes.Goldenrod,
            TileStatus.Disconnected => Brushes.OrangeRed,
            TileStatus.VideoLoss => Brushes.OrangeRed,
            TileStatus.Error => Brushes.Red,
            _ => Brushes.Gray,
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
