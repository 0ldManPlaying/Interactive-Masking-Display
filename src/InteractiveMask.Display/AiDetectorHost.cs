using InteractiveMask.Detection;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace InteractiveMask.Display;

/// <summary>
/// Owns v2.0 AI detector initialisation, per-tile attachment, degradation wiring,
/// and graceful shutdown. Extracted from <see cref="MainWindow"/> so the window
/// delegates lifecycle without owning ONNX details.
/// </summary>
public sealed class AiDetectorHost
{
    private readonly AuditLog _audit;
    private readonly Dispatcher _dispatcher;
    private IObjectDetector? _detector;

    public AiDetectorHost(AuditLog audit, Dispatcher dispatcher)
    {
        _audit = audit;
        _dispatcher = dispatcher;
    }

    /// <summary>Current detector instance, or null while initialising / after failure.</summary>
    public IObjectDetector? Detector => _detector;

    /// <summary>
    /// Loads the ONNX YOLO model and attaches it to every tile. Init failure is
    /// non-fatal: tiles continue with v1.x manual masking only.
    /// </summary>
    public async Task InitializeAsync(IEnumerable<TileViewModel> tiles)
    {
        try
        {
            var detector = new OnnxLocalDetector();
            var config = new DetectorConfig(
                EnabledClasses: new HashSet<ObjectClass>
                {
                    ObjectClass.Person,
                    ObjectClass.TwoWheeler,
                    ObjectClass.Vehicle,
                },
                ConfidenceThresholds: new Dictionary<ObjectClass, float>
                {
                    [ObjectClass.Person]     = 0.15f,
                    [ObjectClass.TwoWheeler] = 0.15f,
                    [ObjectClass.Vehicle]    = 0.15f,
                },
                MaxQueueDepth: 1,
                PreferPolygonMasks: false);
            await detector.InitializeAsync(config).ConfigureAwait(true);

            _detector = detector;
            foreach (var tile in tiles)
            {
                tile.AttachDetector(detector);
                tile.Degradation = detector.Degradation;
            }

            if (detector.Degradation is not null)
            {
                detector.Degradation.StateChanged += OnDegradationStateChanged;
                RefreshDegradationEligibleStreams(tiles);
            }

            _audit.Write(AuditEventType.AiDetectorInit, source: "ai-detector",
                detail: $"{detector.Capability.BackendName}: {detector.Capability.ModelDescription}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.SwallowedException("AiDetectorHost.InitializeAsync", ex);
            _audit.Write(AuditEventType.AiDetectorFault, source: "ai-detector",
                detail: $"init failed, falling back to v1.x masking: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Returns runtime info for the About tab, or unavailable when init failed.</summary>
    public AiRuntimeInfo SnapshotRuntime()
    {
        var detector = _detector;
        if (detector is null)
        {
            return new AiRuntimeInfo(Status: "Unavailable", ModelDescription: null);
        }
        return new AiRuntimeInfo(
            Status: detector.Status.ToString(),
            ModelDescription: detector.Capability.ModelDescription);
    }

    /// <summary>
    /// Feed the adaptive controller the current AI-eligible stream set (slot indices).
    /// </summary>
    public void RefreshDegradationEligibleStreams(IEnumerable<TileViewModel> tiles)
    {
        var degradation = (_detector as OnnxLocalDetector)?.Degradation;
        if (degradation is null) return;

        var eligible = new List<int>();
        foreach (var tile in tiles)
        {
            if (tile.HasCamera && tile.AiEnabled) eligible.Add(tile.SlotIndex);
        }
        degradation.SetEligibleStreams(eligible);
    }

    /// <summary>
    /// Detach tiles and dispose the detector during app shutdown.
    /// </summary>
    public void Shutdown(IEnumerable<TileViewModel> tiles)
    {
        foreach (var tile in tiles)
        {
            tile.AttachDetector(null);
            tile.Degradation = null;
        }

        if (_detector is OnnxLocalDetector localBeforeDispose
            && localBeforeDispose.Degradation is { } degBefore)
        {
            degBefore.StateChanged -= OnDegradationStateChanged;
        }

        if (_detector is null) return;

        try
        {
            Task.Run(() => _detector.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(2));
            _audit.Write(AuditEventType.AiDetectorStopped, source: "ai-detector",
                detail: "graceful dispose");
        }
        catch (Exception ex)
        {
            DiagnosticLog.SwallowedException("AiDetectorHost.Shutdown", ex);
            try
            {
                _audit.Write(AuditEventType.AiDetectorStopped, source: "ai-detector",
                    detail: $"dispose error: {ex.GetType().Name}: {ex.Message}");
            }
            catch (Exception auditEx)
            {
                DiagnosticLog.SwallowedException("AiDetectorHost.Shutdown audit fallback", auditEx);
            }
        }
        finally
        {
            _detector = null;
        }
    }

    private void OnDegradationStateChanged(object? sender, DegradationStateChange e)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => OnDegradationStateChanged(sender, e));
            return;
        }

        // MainWindow owns tile badge updates and audit on degradation transitions;
        // this host only wires the subscription during init.
        DegradationStateChanged?.Invoke(sender, e);
    }

    /// <summary>
    /// Raised on the UI thread when adaptive load changes. MainWindow subscribes
    /// to update badges and audit entries.
    /// </summary>
    public event EventHandler<DegradationStateChange>? DegradationStateChanged;
}
