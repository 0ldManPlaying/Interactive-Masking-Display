namespace InteractiveMask.Display;

/// <summary>
/// Snapshot of the AI detector's current runtime state, sampled by MainWindow
/// when the About tab in Setup asks for it. Used purely for display in the
/// System capabilities card; the underlying detector keeps running regardless
/// of whether anyone is observing it.
/// </summary>
/// <param name="Status">Human-readable status: "Ready", "Initialising", "Unavailable" etc.</param>
/// <param name="ModelDescription">Description string from the detector's
/// <c>Capability.ModelDescription</c> (e.g. "ONNX Runtime (DirectML) + YOLOV8N
/// COCO (centralized worker)"), or null when no detector is wired.</param>
public sealed record AiRuntimeInfo(string Status, string? ModelDescription);
