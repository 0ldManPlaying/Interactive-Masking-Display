using System.Collections.Generic;

namespace InteractiveMask.Detection;

// --------------------------------------------------------------------------
// Detected objects
// --------------------------------------------------------------------------

/// <summary>
/// A single detected object in a frame. <see cref="Class"/> is the masking-categorie
/// that drives Setup-toggles and audit-events; <see cref="RawClassLabel"/> retains the
/// model-native label (for instance "car" or "truck" from a YOLO COCO output) for
/// audit and support diagnostics. Bbox coordinates are in source-frame pixel space.
/// <para>
/// <see cref="Mask"/> is populated when the detector is a segmentation variant
/// (YOLO26n-seg from M3.5 onward); null for bbox-only detectors. The mask data is
/// in bbox-local coordinates and sized to the bbox dimensions, ready for the
/// renderer to apply as an OpacityMask without further resampling.
/// </para>
/// <para>
/// Named <see cref="DetectedObject"/> (not <c>Detection</c>) to avoid an unresolvable
/// type/namespace ambiguity with the enclosing <c>InteractiveMask.Detection</c>
/// namespace at consumer sites.
/// </para>
/// </summary>
public sealed record DetectedObject(
    ObjectClass Class,
    string? RawClassLabel,
    float Confidence,
    BoundingBox Box,
    SegmentationMask? Mask);

/// <summary>
/// Per-detection segmentation mask, sized to the parent <see cref="DetectedObject.Box"/>
/// dimensions and given in bbox-local row-major byte coordinates.
/// </summary>
/// <param name="AlphaData">Row-major byte array of length <c>Width * Height</c>.
/// Each byte is the alpha for the corresponding pixel: 0 means transparent (not
/// part of the object), 255 means fully opaque (part of the object). Intermediate
/// values are allowed but typically the model threshold collapses to {0, 255}.</param>
/// <param name="Width">Width of the mask in pixels; equals parent bbox Width.</param>
/// <param name="Height">Height of the mask in pixels; equals parent bbox Height.</param>
public sealed record SegmentationMask(byte[] AlphaData, int Width, int Height);

/// <summary>
/// Masking-categorie. Drives Setup-toggles and audit policy. The underlying model can
/// produce more fine-grained labels (see <see cref="DetectedObject.RawClassLabel"/>) but
/// those are not used to drive privacy decisions.
/// </summary>
public enum ObjectClass
{
    Unknown      = 0,
    Face         = 1,
    Person       = 2,
    TwoWheeler   = 3,   // bicycle + motorcycle
    Vehicle      = 4,   // car + bus + truck
    LicensePlate = 5,
}

/// <summary>
/// Axis-aligned bounding box in frame-pixel coordinates. Origin (0, 0) is top-left.
/// </summary>
public readonly record struct BoundingBox(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>
/// Polygon outline in frame-pixel coordinates. Used for segmentation-mode masks;
/// bbox-only detectors leave <see cref="DetectedObject.Mask"/> null. The polygon is
/// assumed closed (last point connects back to first); implementations should not
/// duplicate the closing point.
/// </summary>
public sealed record Polygon(IReadOnlyList<PolygonPoint> Points);

/// <summary>Pixel-space vertex of a <see cref="Polygon"/>. Named <c>PolygonPoint</c>
/// rather than <c>Point</c> to avoid clashing with <c>System.Windows.Point</c> at
/// consumer sites that import both WPF and this namespace.</summary>
public readonly record struct PolygonPoint(int X, int Y);

// --------------------------------------------------------------------------
// Detector lifecycle and metadata
// --------------------------------------------------------------------------

/// <summary>Operational state of a detector backend. Drives UI status indicators.</summary>
public enum DetectorStatus
{
    Uninitialized = 0,
    Initializing  = 1,
    Ready         = 2,
    Degraded      = 3,   // running but in adaptive-degradation mode
    Unavailable   = 4,   // permanent fault for this session; render falls back to v1.x masking
}

/// <summary>Per-implementation metadata. Pluggable backends provide their own descriptor.</summary>
public sealed record DetectorCapability(
    string BackendName,
    string ModelDescription,
    IReadOnlyList<ObjectClass> SupportedClasses,
    bool SupportsPolygonMasks);

/// <summary>
/// Configuration handed to <see cref="IObjectDetector.InitializeAsync"/>. Tells the
/// detector which categories to keep in its output, the confidence threshold per
/// category (a class disabled in Setup is excluded from <see cref="EnabledClasses"/>),
/// and execution hints.
/// </summary>
public sealed record DetectorConfig(
    IReadOnlySet<ObjectClass> EnabledClasses,
    IReadOnlyDictionary<ObjectClass, float> ConfidenceThresholds,
    int MaxQueueDepth,
    bool PreferPolygonMasks);

// --------------------------------------------------------------------------
// Per-frame I/O
// --------------------------------------------------------------------------

/// <summary>
/// Wraps the decoded frame plus the IDIS GDK timestamp so detector and renderer can
/// align on the same logical frame. Concrete implementations of FrameRef hold either
/// a managed bitmap, a pinned native pointer, or a GPU resource handle depending on
/// the backend. The base record keeps the descriptor minimal; specific backends
/// derive their own typed FrameRef with the buffer reference they need.
/// </summary>
public abstract record FrameRef(long TimestampTicks, int Width, int Height, int StreamId);

/// <summary>Per-frame metrics emitted alongside each <see cref="DetectionFrame"/>.</summary>
public sealed record DetectorMetrics(
    double InferenceLatencyMs,
    int QueueDepth,
    int? GpuUtilizationPercent);   // null when not measurable

/// <summary>Output of one detection pass; one per submitted <see cref="FrameRef"/>.</summary>
public sealed record DetectionFrame(
    long FrameTimestampTicks,
    int StreamId,
    IReadOnlyList<DetectedObject> Detections,
    DetectorMetrics Metrics);
