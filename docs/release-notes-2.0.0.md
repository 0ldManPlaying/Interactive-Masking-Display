# InteractiveMask v2.0.0 — Release Notes

**Release date:** 2026-05-11
**Publisher:** IDIS Nederland BV
**Previous release:** [v1.3.0](release-notes-1.3.0.md)

---

## TL;DR

v2.0 introduces **AI-driven object masking** as the headline feature: live YOLO26s-seg segmentation runs locally on the GPU via DirectML and applies privacy masks to the detected objects rather than the entire tile. Operators can pick categories per camera (Person / TwoWheeler / Vehicle), tune confidence thresholds, draw region-of-interest polygons, and choose between color-coded silhouettes or a classic source-blur (CCTV) look — all per camera, all persisted, all with audit-log coverage.

This is a major release. The detection pipeline, rendering pipeline, persistence schema, and AI-settings UI are all new. The classic 1.x behavior (full-tile manual blur masks) is fully preserved as the default when AI is off.

---

## What's new

### AI-driven privacy masking (headline feature)

- **YOLO26s-seg model** runs locally on the GPU through ONNX Runtime DirectML. NMS-free decoder with 300 anchor-free detection slots, 32-channel prototype masks at 160×160, sigmoid activation. Targets ~25–35 ms inference on mid-range Windows GPUs.
- **Centralized `InferenceCoordinator`** — single background worker per camera, channel-based wake-up with slot-replacement semantics. Solves the native ORT crashes that occurred under concurrent inference attempts in earlier prototypes.
- **Three object categories**, color-coded in the live view:
  - **Person** — red (`#E74C3C`)
  - **Two-wheeler** (bike / motorcycle) — orange (`#F39C12`)
  - **Vehicle** (car / truck / bus) — blue (`#3498DB`)
- **Per-camera enable + class selection** — operators decide for each camera independently which categories get masked. Cameras with AI disabled run the classic full-tile manual-blur path with zero overhead.
- **Per-camera confidence slider** (15–70 %, default 40 %) with **hysteresis filtering**: an object stays rendered down to half the threshold once locked on, with IoU ≥ 0.4 matching to the previous frame. Removes the on/off flicker on borderline detections in the background of a scene.
- **Per-camera Region of Interest (ROI) polygon** — draw a polygon directly on a live snapshot to restrict detection to a specific area; detections outside the ROI are not masked. Drag-to-move on existing vertices. Skip the polygon to use the whole camera.
- **Per-camera mask-padding slider** (0–50 %) — enlarges the mask region around each detection to compensate for tight bounding boxes that don't fully cover the object.
- **Per-camera mask-style toggle** — switch between:
  - **Color-coded silhouette** (default): solid per-class color clipped by the segmentation mask, then heavily Gaussian-blurred. Operator can tell category at a glance.
  - **Source blur (CCTV look)**: the camera area itself is sampled through a Gaussian blur — features are unrecognizable but the visual feels like a classic CCTV redaction.
- **Per-camera mask opacity slider** (20–100 %) — make the mask layer more transparent so background context shows through. Useful in situations where operators need to see *that* a person is there without seeing *who* they are.

### AI-settings dialog — fully redesigned

The per-camera AI dialog has been rebuilt in the app's card-style language. Sections:

1. **Enable AI masking** — toggle switch.
2. **Categories** — color-coded pill chips (Person / TwoWheeler / Vehicle).
3. **Detection threshold** — slider with live percentage readout.
4. **Mask padding** — slider 0–50 %.
5. **Mask style** — segmented toggle (Color-coded / Source blur).
6. **Mask opacity** — slider 20–100 %.
7. **Region of Interest** — point count + "Draw ROI…" button opening the polygon editor on a live snapshot.

All controls are fully localized in NL / EN / DE / FR / ES.

### Operator-facing telemetry

- **Audit-log events** for the full AI lifecycle: model load / unload, per-camera enable / disable, ROI edits, confidence-threshold changes. Same audit log used for privacy-mask events in 1.x.
- **About tab → AI runtime info**: live model name, execution provider, average inference latency, frame counter. Lets the operator verify the AI pipeline is actually running on the GPU and not silently falling back to CPU.

### Foundational improvements (carried from 1.3.x late-cycle work)

- **ONNX Runtime DirectML** bumped 1.20.1 → 1.24.4 — adds DirectML kernel coverage for ops that YOLO11 / YOLO26 models depend on.
- **Setup window** widened 1180 → 1280 px to give the richer per-camera column more breathing room.
- **Segmentation rendering** via WPF `OpacityMask` + per-class `ImageBrush` — privacy and category-preview in a single pass.

---

## Breaking changes & migration notes

v2.0 is backward-compatible with v1.3 configurations — the new per-camera AI fields read back as their defaults when missing — but the surrounding architecture has changed enough to call out:

### Configuration schema

New per-camera fields in `CameraSlotSettings` (persisted in `config.json`):

| Field | Type | Default | Range |
|---|---|---|---|
| `AiEnabled` | bool | `false` | — |
| `AiClasses` | string[] | `[]` | Person / TwoWheeler / Vehicle |
| `AiConfidencePercent` | int | `40` | 15–70 |
| `MaskPaddingPercent` | int | `0` | 0–50 |
| `AiRoiPolygon` | PolygonPoint[] | `[]` | — |
| `AiUseSourceBlur` | bool | `false` | — |
| `AiMaskOpacityPercent` | int | `100` | 20–100 |

Older config files load unchanged; the new fields take their defaults until the operator opens AI Settings and saves.

### Threading model

The detection pipeline is now driven by a single `InferenceCoordinator` per camera rather than ad-hoc background tasks. Custom integrations that called the detection layer directly from earlier prototypes need to route through the coordinator's `EnqueueFrame()` instead — multiple concurrent ORT calls into the same session are not supported and will crash the native layer.

### Dependency bump

- `Microsoft.ML.OnnxRuntime.DirectML` **1.20.1 → 1.24.4**

The 1.24.4 build requires Windows 10 1903+ and a DirectML 1.13-capable GPU driver. All currently-supported Windows endpoints meet this; very old test machines with pre-2020 drivers may need a driver update.

### Default behavior

If no AI configuration is present, behavior is **identical to v1.3.0** — manual full-tile blur masks driven by operator taps, no model load, no GPU work. AI is strictly opt-in per camera.

---

## Acceptance tests (v2.0 release gate)

| Test | Expected |
|---|---|
| Fresh install with no AI configured | Behaves exactly like v1.3.0 |
| Enable AI on one camera with default settings | Person / Vehicle silhouettes appear in the live tile within 5 s |
| Toggle mask style Color-coded → Source blur | Live tile switches visual style without restart |
| Adjust opacity 100 % → 50 % | Mask becomes visibly translucent, background bleeds through |
| Draw ROI restricting detection to a doorway | Detections outside the polygon are not masked |
| Open About tab while AI is running | Shows non-zero inference latency and frame counter |
| Restart kiosk | All per-camera AI settings persist exactly as configured |
| Disable AI on a camera | GPU usage drops; tile reverts to classic full-tile blur on tap |

---

## Known limitations

- **GPU required.** AI masking needs a DirectML-capable GPU. CPU fallback exists but is too slow for live (≤ 5 fps); operators should disable AI on machines without a GPU.
- **Three-class taxonomy.** Only Person, TwoWheeler, and Vehicle are exposed today. Adding categories (face-only, license plate, etc.) is a v2.1 candidate.
- **No NVR-side recording integration.** Masks are applied to the live display only; the NVR continues to record the unmasked stream as configured on the recorder itself.

---

## Upgrade path from v1.3.x

1. Install the v2.0.0 MSI over the existing 1.3.x installation. Config and audit log are preserved.
2. On first launch, behavior is identical to v1.3.0.
3. Open Setup → Cameras → click the **AI…** button on any camera row to opt in per camera.

No data migration is required.

---

## Credits

YOLO26s-seg model architecture: Ultralytics (AGPL-3.0).
DirectML execution provider: Microsoft ONNX Runtime.
Healthcare-grade privacy review and acceptance testing: IDIS Nederland BV.
