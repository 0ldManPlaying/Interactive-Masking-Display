using InteractiveMask.Detection;
using InteractiveMask.Gdk;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
    private string _nvrTitle = "";
    private bool _showLabel = true;
    private bool _showNvrTitle;
    private string _statusText = "";
    private Brush _statusBrush = Brushes.Gray;
    private bool _hasCamera;
    private bool _isMasked;
    private TileStatus _status = TileStatus.Empty;
    private DateTime? _maskedAtUtc;
    private int _autoUnmaskMinutes;
    /// <summary>
    /// Symmetric counterpart of <see cref="_maskedAtUtc"/> for the privacy-
    /// default mode: timestamp at which the tile transitioned from masked
    /// to revealed. Combined with <see cref="_autoReMaskMinutes"/> drives
    /// the auto-rollback back to masked.
    /// </summary>
    private DateTime? _revealedAtUtc;
    private int _autoReMaskMinutes;
    private bool _isTimerWarning;
    private string _countdownText = "";

    // -- v2.0 AI detection wiring ---------------------------------------------
    // The detector is shared across all tiles and owned by MainWindow; we hold
    // a weak reference here in the form of a plain field set via AttachDetector.
    // Frame submission is rate-limited per tile so 16 tiles do not collectively
    // saturate the inference pipeline.
    private IObjectDetector? _detector;
    private int _frameCount;
    private IReadOnlyList<DetectedObject> _detections = Array.Empty<DetectedObject>();
    private const int DetectorEveryNFrames = 8;

    /// <summary>
    /// Per-camera v2.0 AI on/off. When false, OnFrameDecoded bypasses the detector
    /// submission path entirely so cameras pointing at irrelevant scenes (lawns,
    /// walls, ceilings) don't waste GPU. Mirrors CameraSlotSettings.AiEnabled and
    /// is pushed from MainWindow.BindCameraToSlot at camera-attach time.
    /// </summary>
    public bool AiEnabled { get; set; } = true;

    /// <summary>
    /// Per-camera v2.0 AI category filter. Detections of classes not in this set
    /// are dropped before being rendered. Mirrors CameraSlotSettings.AiClasses.
    /// Empty set effectively disables all detections (same effect as AiEnabled=false
    /// but with the submission path still active - reserved for future ad-hoc
    /// global suppression).
    /// </summary>
    public IReadOnlySet<ObjectClass> AiClasses { get; set; } = new HashSet<ObjectClass>
    {
        ObjectClass.Person,
        ObjectClass.TwoWheeler,
        ObjectClass.Vehicle,
    };

    /// <summary>
    /// Per-camera bbox inflation percent (0..50). Applied at render time to grow
    /// the privacy blur slightly past the model's tight bbox, compensating for
    /// the few pixels of detection slack that YOLO sometimes shows around object
    /// silhouettes. Stored detections (audit, persistence) keep the raw values;
    /// only the rendered overlay sees the inflation.
    /// </summary>
    public int MaskPaddingPercent { get; set; } = 10;

    /// <summary>
    /// Timestamp of the last non-empty detection result. Combined with
    /// <see cref="EmptyDetectionGrace"/> it rides out single-frame false negatives:
    /// when the detector returns no detections on a frame but we had detections
    /// very recently, we keep the existing boxes visible briefly instead of
    /// flickering them off. Particularly noticeable on static objects (parked
    /// vehicles) whose confidence wavers per-frame around the threshold.
    /// </summary>
    private DateTime _lastDetectionsUpdatedUtc = DateTime.MinValue;
    private static readonly TimeSpan EmptyDetectionGrace = TimeSpan.FromMilliseconds(750);

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

    /// <summary>
    /// Camera title as configured on the NVR (G2DEVICE_STATUS._camera_desc).
    /// Updated by MainWindow after each connect/sync; empty until the first
    /// device-status callback succeeds. Independent of the operator-typed
    /// <see cref="Label"/>.
    /// </summary>
    public string NvrTitle
    {
        get => _nvrTitle;
        set
        {
            if (Set(ref _nvrTitle, value ?? ""))
            {
                OnPropertyChanged(nameof(IsNvrTitleVisible));
            }
        }
    }

    /// <summary>
    /// Whether the operator label is rendered on the tile bottom bar.
    /// Driven by PrivacySettings.ShowCameraLabel.
    /// </summary>
    public bool ShowLabel
    {
        get => _showLabel;
        set
        {
            if (Set(ref _showLabel, value))
            {
                OnPropertyChanged(nameof(IsLabelVisible));
            }
        }
    }

    /// <summary>
    /// Whether the NVR-side camera title is rendered on the tile bottom bar.
    /// Driven by PrivacySettings.ShowNvrTitle.
    /// </summary>
    public bool ShowNvrTitle
    {
        get => _showNvrTitle;
        set
        {
            if (Set(ref _showNvrTitle, value))
            {
                OnPropertyChanged(nameof(IsNvrTitleVisible));
            }
        }
    }

    /// <summary>True when the operator label should be visible (toggle on AND not empty).</summary>
    public bool IsLabelVisible => _showLabel && !string.IsNullOrWhiteSpace(_label);

    /// <summary>True when the NVR title should be visible (toggle on AND not empty).</summary>
    public bool IsNvrTitleVisible => _showNvrTitle && !string.IsNullOrWhiteSpace(_nvrTitle);

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

    /// <summary>
    /// Apply or remove the privacy mask. The same auto-timer parameter feeds
    /// either an auto-unmask (when <paramref name="masked"/> is true, oversight
    /// default mode) or an auto-re-mask (when <paramref name="masked"/> is false,
    /// privacy default mode); only one direction is ever live at a time.
    /// </summary>
    /// <param name="autoMinutes">Minutes after which the tile auto-transitions back. 0 = no timer.</param>
    /// <param name="anchorUtc">Optional anchor for the timer. Defaults to UtcNow.</param>
    public void SetMasked(bool masked, int autoMinutes = 0, DateTime? anchorUtc = null)
    {
        IsMasked = masked;
        if (masked)
        {
            _autoUnmaskMinutes = Math.Max(0, autoMinutes);
            _maskedAtUtc = _autoUnmaskMinutes > 0 ? (anchorUtc ?? DateTime.UtcNow) : null;
            // Clear the inverse-direction timer; only one auto path is ever live.
            _revealedAtUtc = null;
            _autoReMaskMinutes = 0;
            IsTimerWarning = false;
            CountdownText = "";
        }
        else
        {
            _autoReMaskMinutes = Math.Max(0, autoMinutes);
            _revealedAtUtc = _autoReMaskMinutes > 0 ? (anchorUtc ?? DateTime.UtcNow) : null;
            _autoUnmaskMinutes = 0;
            _maskedAtUtc = null;
            IsTimerWarning = false;
            CountdownText = "";
        }
    }

    /// <summary>
    /// Called once per second by the global ticker (oversight-default mode).
    /// Updates <see cref="CountdownText"/> and <see cref="IsTimerWarning"/>;
    /// returns true when the timer has expired and the tile should be
    /// auto-unmasked (caller must run the unmask logic).
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

    /// <summary>
    /// Called once per second by the global ticker (privacy-default mode).
    /// Mirror of <see cref="TickAutoUnmask"/>: returns true when a revealed
    /// tile has been visible long enough and should snap back to masked.
    /// </summary>
    public bool TickAutoReMask(int warningMinutes)
    {
        if (IsMasked || _revealedAtUtc is null || _autoReMaskMinutes <= 0)
        {
            if (IsTimerWarning) IsTimerWarning = false;
            if (CountdownText.Length > 0) CountdownText = "";
            return false;
        }

        var elapsed = DateTime.UtcNow - _revealedAtUtc.Value;
        var total = TimeSpan.FromMinutes(_autoReMaskMinutes);
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
        OnPropertyChanged(nameof(IsLabelVisible));
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
        // The detector reference stays wired (it survives camera-rebind); only
        // the cached detections become stale and need clearing.
        Detections = Array.Empty<DetectedObject>();
    }

    /// <summary>
    /// Latest detection result for this tile. Updated asynchronously from the
    /// detector backend; rendered as a per-detection overlay in <c>MainWindow.xaml</c>.
    /// </summary>
    public IReadOnlyList<DetectedObject> Detections
    {
        get => _detections;
        private set
        {
            if (ReferenceEquals(_detections, value)) return;
            _detections = value;
            OnPropertyChanged(nameof(Detections));
        }
    }

    /// <summary>
    /// Wires this tile to the shared AI detector. Called once at startup from
    /// <c>MainWindow</c> after the detector finishes <see cref="IObjectDetector.InitializeAsync"/>.
    /// Pass <c>null</c> to detach (used when the detector faults or shuts down).
    /// </summary>
    public void AttachDetector(IObjectDetector? detector)
    {
        _detector = detector;
        if (detector is null)
        {
            Detections = Array.Empty<DetectedObject>();
        }
    }

    private void OnFrameDecoded(DecodedFrame frame)
    {
        // Called on GDK thread. Marshal the bitmap update to the UI thread.
        // BeginInvoke (not Invoke) so the GDK callback can return immediately and the
        // next frame's decode pipeline isn't blocked on UI work.
        _dispatcher.BeginInvoke(() => RenderFrame(frame), DispatcherPriority.Render);

        // v2.0 AI path: every Nth frame, snapshot the unmanaged pixel buffer to a
        // managed array (must happen synchronously here on the GDK thread; the
        // buffer is reused for the next frame) and submit asynchronously to the
        // shared detector. Skipped when masked because a masked tile shows blur,
        // not the underlying image, and per-detection overlays would be invisible.
        var detector = _detector;
        if (detector != null && AiEnabled && !_isMasked && detector.Status == DetectorStatus.Ready)
        {
            _frameCount++;
            if (_frameCount % DetectorEveryNFrames == 0)
            {
                int byteLen = frame.Stride * frame.Height;
                var bgra = new byte[byteLen];
                Marshal.Copy(frame.Buffer, bgra, 0, byteLen);
                var frameRef = BitmapFrameRef.FromBgra(
                    timestampTicks: DateTime.UtcNow.Ticks,
                    width: frame.Width,
                    height: frame.Height,
                    streamId: SlotIndex,
                    bgraPixels: bgra);
                _ = SubmitDetectionAsync(detector, frameRef);
            }
        }
    }

    private async Task SubmitDetectionAsync(IObjectDetector detector, BitmapFrameRef frameRef)
    {
        try
        {
            var result = await detector.DetectAsync(frameRef).ConfigureAwait(false);
            // BeginInvoke returns a DispatcherOperation we intentionally don't await;
            // the UI thread will pick up the assignment on its next pulse.
            _ = _dispatcher.BeginInvoke(() => ApplyDetections(result.Detections));
        }
        catch
        {
            // Detector path failures must not affect the render flow. The Status
            // event on the detector will surface persistent faults; transient
            // exceptions per frame are swallowed.
        }
    }

    /// <summary>
    /// UI-thread entry point that applies a fresh detection result to this tile.
    /// Implements a small grace-window so a single-frame false negative does not
    /// flicker the overlay off: when the new result is empty but we had detections
    /// very recently we keep the previous ones visible until the grace expires.
    /// Non-empty results always replace immediately and reset the timer.
    /// </summary>
    private void ApplyDetections(IReadOnlyList<DetectedObject> fresh)
    {
        // Filter to the per-camera class set. Detector emits everything it
        // supports; rejecting unwanted classes here keeps the configuration
        // contract simple: tiles decide what they render, the detector stays
        // model-agnostic.
        if (fresh.Count > 0 && AiClasses.Count < 3)
        {
            // Fast-path: only allocate a new list when filtering is actually
            // going to drop something. When all three classes are enabled the
            // common case is "pass through".
            fresh = fresh.Where(d => AiClasses.Contains(d.Class)).ToList();
        }

        // Inflate bboxes by the per-camera padding percent so the privacy
        // blur extends slightly past the detection. Inflation happens here
        // (post-detector, pre-render) so audit and any future analytics keep
        // the raw bbox; only the on-screen overlay sees the padded version.
        if (fresh.Count > 0 && MaskPaddingPercent > 0)
        {
            int srcW = _bitmap?.PixelWidth ?? int.MaxValue;
            int srcH = _bitmap?.PixelHeight ?? int.MaxValue;
            float frac = MaskPaddingPercent / 100f;
            fresh = fresh.Select(d => InflateForRender(d, frac, srcW, srcH)).ToList();
        }

        if (fresh.Count > 0)
        {
            Detections = fresh;
            _lastDetectionsUpdatedUtc = DateTime.UtcNow;
            return;
        }
        // Empty result. Keep existing detections visible if we are still inside
        // the grace window; otherwise accept the empty (subject genuinely gone)
        // and clear the overlay.
        if (_detections.Count > 0 && DateTime.UtcNow - _lastDetectionsUpdatedUtc < EmptyDetectionGrace)
        {
            return;
        }
        Detections = fresh;
    }

    private static DetectedObject InflateForRender(DetectedObject d, float frac, int srcW, int srcH)
    {
        int padX = (int)(d.Box.Width * frac);
        int padY = (int)(d.Box.Height * frac);
        int x = Math.Max(0, d.Box.X - padX);
        int y = Math.Max(0, d.Box.Y - padY);
        int w = Math.Min(srcW - x, d.Box.Width + 2 * padX);
        int h = Math.Min(srcH - y, d.Box.Height + 2 * padY);
        if (w <= 0 || h <= 0) return d;
        return d with { Box = new BoundingBox(x, y, w, h) };
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
