# InteractiveMask v2.0.3 — Release Notes

**Release date:** 2026-05-26  
**Previous release:** v2.0.2

---

## TL;DR

v2.0.3 is a maintainability and quality release: automated unit tests for core auth/config/geometry/degradation logic, focused extractions from `MainWindow`, `CancellationTokenSource` disposal fixes, lightweight diagnostic logging on previously silent catch paths, and documentation alignment for the default YOLO26 model.

---

## What's new

### Automated tests (`InteractiveMask.Tests`)

- xUnit project added to the solution.
- Coverage for `PointInPolygonHelper`, `MaskController` remote auth branches, `DegradationController` degrade/restore ladder, and legacy single-NVR → `Nvrs[]` config migration round-trip.

### Maintainability

- **`NvrConnectionManager`** — owns multi-NVR sessions, slot bindings, live camera/NVR fleet reconciliation, and reconnect banner aggregation.
- **`AiDetectorHost`** — owns ONNX detector init/shutdown, tile attachment, and degradation subscription wiring.

### Resource hygiene

- `IpcServer.Dispose()` now disposes its `CancellationTokenSource` after cancel (matches `IpcClient` / `NvrSession` patterns).

### Observability

- **`DiagnosticLog`** in Display (Debug output) for swallowed faults on audit/config paths.
- Detection worker silent catches now log via `Debug.WriteLine` instead of failing silently.

### Documentation

- README updated to describe the default **YOLO26n-seg** model and optional **yolo26s-seg** drop-in, matching `ResolveYoloModelPath` preference order.

---

## Upgrade notes

No configuration migration beyond prior v2.0.x releases. Rebuild and redeploy the Display + WebHost assemblies together so version metadata stays aligned.
