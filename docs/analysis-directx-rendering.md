# Analysis — could InteractiveMask be substantially faster via DirectX?

**Status:** exploration / discussion document. No code changes.
**Audience:** development team (Joris + lead).
**Date:** May 2026.

A team member suggested the application could be "much faster and more efficient" if it used DirectX. This document maps what that would actually mean in our codebase, what we are already doing on the GPU today, where the bottlenecks actually live, what each plausible DirectX-based change would buy us, and what each change would cost. It deliberately stops short of recommending an implementation — the goal is to give the team enough material to decide together.

---

## 1. What "use DirectX" can mean here

"DirectX" is a brand for a family of APIs. In our context it can mean any of the following — and they are very different decisions:

| Variant | What it touches | Already in use? |
|---|---|---|
| **DirectML** (D3D12-based ML runtime) | AI inference on the GPU | **Yes** — ONNX Runtime's DirectML execution provider runs YOLO26s-seg on the GPU today |
| **DXVA / D3D11VA** (hardware video decode) | H.264 / H.265 → YUV decode | **Unknown** — the IDIS GDK does the decode in `g2decoder.decompress`; whether it uses DXVA internally is not exposed by the public C# API |
| **D3D11 / D3D12 swap chain** for the live tile surface | Replace `WriteableBitmap` with a GPU-resident surface bound via `D3DImage` or `SwapChainPanel` | **No** — current rendering uses `WriteableBitmap` (managed memory → DirectComposition surface) |
| **Custom HLSL pixel shader** for the privacy blur | Replace WPF's `BlurEffect` with a hand-tuned shader | **No** — `BlurEffect Radius="20"` (lowered from 80 in v2.0.1) is WPF's built-in effect, which compiles to an HLSL shader but with WPF's render-target overhead per tile |
| **Direct2D** for 2D drawing | The detection overlays, badges, status text | **No** — every overlay is currently WPF `Shape` / `TextBlock` |
| **WinUI 3 / WindowsAppSDK** with `SwapChainPanel` | Total UI-stack swap | **No** — current stack is WPF (.NET 9 + `UseWPF`) |

When the dev says "use DirectX", we should clarify with them which variant they mean. The answer changes the conversation by an order of magnitude.

---

## 2. The current render pipeline, end-to-end

```
┌─────────────────────────────────────────────────────────────────────────┐
│  IDIS NVR  ──H.264/H.265──▶  IDIS GDK native                            │
│                              ├─ g2watch (TCP session)                   │
│                              └─ g2decoder.decompress  (software → YV12) │
│                                                                         │
│                              g2decoder.picture_scale (software → BGRA)  │
│                              ─────────────────────────                  │
│                              YV12 + BGRA buffers live in NATIVE memory  │
│                              (allocated by us, pointer handed to GDK)   │
│                                                                         │
│                              FrameDecoded event fired on GDK thread     │
└─────────────────────────────────────────────────────────────────────────┘
                                            │
                       (cross-thread via Dispatcher.BeginInvoke)
                                            │
┌─────────────────────────────────────────────────────────────────────────┐
│  Display (WPF, UI thread)                                               │
│                                                                         │
│  TileViewModel.RenderFrame:                                             │
│    Bitmap.WritePixels(rect, frame.Buffer, stride*height, stride);       │
│                                                                         │
│    ⇒ CPU memcpy from the native BGRA buffer to the WriteableBitmap's    │
│      back-buffer, which is a DirectComposition surface owned by         │
│      MIL / DWM.                                                         │
│                                                                         │
│    WPF render pass: DWM composites the tile onto the screen — this      │
│    IS GPU-accelerated already. The BlurEffect on masked tiles runs      │
│    as an HLSL shader inside MIL, also GPU.                              │
└─────────────────────────────────────────────────────────────────────────┘

(parallel branch for AI)

┌─────────────────────────────────────────────────────────────────────────┐
│  TileViewModel.OnFrameDecoded (GDK thread)                              │
│                                                                         │
│    bgra = ArrayPool<byte>.Shared.Rent(stride*height);          // ~8MB  │
│    Marshal.Copy(frame.Buffer, bgra, 0, byteLen);                        │
│    InferenceCoordinator.Submit(slot, BitmapFrameRef, callback);         │
│                                                                         │
│  InferenceCoordinator worker (thread-pool):                             │
│    Preprocess  (CPU resize + normalise → 3·640·640 float tensor)        │
│    ─────────────────────────────────────────                            │
│    ONNX Runtime + DirectML EP                                           │
│    ⇒ tensor uploaded to D3D12 resource, inference on GPU                │
│    ─────────────────────────────────────────                            │
│    Decode  (CPU NMS-free decoder + mask combine, 32 prototypes)         │
│    Detections → callback → UI thread → tile overlay render              │
└─────────────────────────────────────────────────────────────────────────┘
```

### Where the time actually goes (best estimate without a profile run)

For a single tile at 1080p25 with AI on:

| Stage | Cost / frame | Where it runs |
|---|---|---|
| GDK decompress (H.264 → YV12) | ~3-8 ms | CPU, GDK-internal |
| `picture_scale` (YV12 → BGRA, bilinear) | ~2-4 ms | CPU, GDK-internal |
| **`Marshal.Copy` of BGRA buffer for AI submission** | ~1-2 ms at 1080p, ~4-8 ms at 4K | CPU, hot path |
| **`WriteableBitmap.WritePixels`** | ~1-3 ms at 1080p, ~4-10 ms at 4K | CPU memcpy + GPU upload |
| WPF compose + BlurEffect (when masked) | ~0.2-0.6 ms / tile | GPU |
| ONNX inference (when AI on) | ~25-35 ms (YOLO26s-seg on RTX 4060) | GPU (DirectML) |
| ApplyDetections + per-detection overlay render | ~1-3 ms | UI thread + GPU |

For 16 tiles in a 4×4 grid, that's `16 × 1080p frame cost = 16 × (~10 ms) ≈ 160 ms of CPU work per "frame round"` — but per tile we only see frames at the tile's actual frame-rate, not 16 per round, so the total work is the sum of each tile's stream rate.

**The two clearly biggest software costs** are:
1. The `picture_scale` YV12→BGRA conversion (CPU, inside GDK)
2. The `Marshal.Copy` + `WriteableBitmap.WritePixels` (CPU memcpy and CPU→GPU upload)

The AI inference is already GPU-accelerated and is the right kind of latency given the model size.

---

## 3. We are already using "DirectX" — for the part that matters most

It is worth saying explicitly: **ONNX Runtime's DirectML execution provider IS DirectX.** It runs YOLO26s-seg on the GPU via D3D12 + DirectML. The 25–35 ms inference latency we measure is real GPU work, not CPU. Switching that path to anything else (CUDA EP, TensorRT, raw HLSL compute shaders) would not produce a meaningful speed-up for this model on Windows + dGPU, and would lose multi-vendor support (Intel Arc, AMD).

Any "speed up the AI" conversation should be reframed as a model-architecture conversation (smaller model, lower input resolution, fewer detection slots), not an API conversation.

---

## 4. The realistic DirectX-shaped wins

This is the meat of the analysis. Below are five candidates, ordered by ratio of gain to effort.

### 4.1. Replace `WriteableBitmap.WritePixels` with `D3DImage` + shared D3D11 texture

**What it is.** WPF's `D3DImage` accepts a `D3D9Ex` shared surface (with NV interop for D3D11/12). Instead of memcpy'ing BGRA into a WriteableBitmap, we'd write directly into a GPU texture, and the WPF compositor would consume that texture directly without an additional copy.

**Gain.** Eliminates the per-frame CPU→GPU upload. At 4K × 16 tiles that's potentially 80–150 MB/s of saved memory bandwidth plus 4–10 ms/tile/frame of CPU time. On a populated 4K grid this could be the difference between smooth and dropping frames.

**Cost.** Significant. We'd need:
- A `Vortice.Windows` or `SharpDX` (the latter is unmaintained) dependency, ~5 MB native interop layer.
- D3D11 device + immediate context management, per-tile shared surface allocation, surface synchronisation against the GDK thread.
- A custom `D3DImage`-based control replacing the `<Image>` in `MainWindow.xaml`.
- All of this still needs the BGRA pixels in CPU memory first (because GDK delivers to a managed buffer), so the saving is the upload step only, not the copy.
- The full win requires the next item.

**Realistic gain without the next item:** probably 2-5 ms/tile/frame on 4K. **Effort:** 1–2 weeks for one developer to get it stable.

### 4.2. Get GDK to deliver frames as D3D11 textures (zero-copy)

**What it is.** Modify the way we receive frames from the IDIS GDK so that the decoded surface is *already* on the GPU — no CPU memory involved. The GDK would expose a D3D11 texture handle (e.g., via DXGI shared handle or NV12 surface) and our pipeline would never touch BGRA in managed memory.

**Gain.** The complete elimination of the CPU→GPU upload — combined with #4.1 this is the maximum possible improvement to the render path. Potentially 5–10× lower CPU usage on a populated grid.

**Cost.** Depends entirely on the IDIS GDK API. The C# binding we have (`g2decoder`) exposes `decompress` and `picture_scale` returning managed buffers. To get a D3D11 surface out, the GDK either has to expose a different API (which it may or may not have — to be confirmed with IDIS) or we have to write a separate decode pipeline using Media Foundation / DXVA on top of the raw H.264 stream, **completely bypassing the GDK's decoder** but keeping its session / camera / discovery features.

If the GDK doesn't expose this, the cost is essentially "write your own video decoder", which is a multi-month project and a substantial risk to the product's stability.

**Realistic gain:** very large (maybe halve the CPU on a 4×4 grid). **Effort:** weeks if GDK supports it, months if not. **Risk:** high — we lose the GDK's tested decode path.

### 4.3. Replace WPF `BlurEffect` with a custom HLSL pixel shader

**What it is.** WPF's `BlurEffect` is implemented in MIL as an HLSL shader, but every effect application costs WPF a render-target switch and intermediate buffer per element. For 16-25 tiles all running a Gaussian blur simultaneously (the mass-mask scenario that prompted the v2.0.1 fix), the overhead adds up.

A bespoke shader could:
- Use a separable Gaussian (two passes of 1D blurs) which is much cheaper than naive 2D for the same visual quality
- Compose all per-detection overlays into one screen-space pass instead of WPF doing one pass per Rectangle element
- Use `Compute Shader` for higher throughput than the pixel-shader path

**Gain.** Moderate. We already lowered the BlurEffect radius from 80 to 20 in v2.0.1 to address the mass-mask sluggishness, and that fix alone got us most of the way. A bespoke shader on top of that might be 2–3× faster again, but it's diminishing returns relative to what the operator can perceive.

**Cost.** Custom shader maintenance, shader compilation pipeline, debugging tooling (RenderDoc), platform compatibility testing. Worth doing only if profiling shows the post-v2.0.1 blur is still the bottleneck — which we don't currently believe.

**Realistic gain:** small-to-moderate on top of v2.0.1's Radius=20. **Effort:** ~1 week. **Recommended only if profiling justifies it.**

### 4.4. Direct2D for the detection-overlay layer

**What it is.** Move the per-detection coloured silhouettes and the source-blur rectangles out of the WPF visual tree (each currently a `Rectangle` with bindings + an `OpacityMask` + a `BlurEffect`) and onto a Direct2D surface drawn under one shared `D3DImage`.

**Gain.** Avoids dozens of WPF visuals per tile when there are many detections. For a busy parking-lot camera with 10+ vehicles in frame, this could reduce the overlay-render cost noticeably.

**Cost.** Same interop layer as #4.1; plus drawing-state management; plus the layout-math we currently do via WPF (`Canvas.Left`, `Canvas.Top`, `Width`, `Height`, `Viewbox` letterbox) has to be done by hand.

**Realistic gain:** small in typical scenes (1-3 detections); moderate in busy scenes. **Effort:** ~1 week.

### 4.5. Hardware video decode via Media Foundation (no GDK changes)

**What it is.** Bypass `g2decoder.decompress` for the H.264/H.265 → YV12 step. Use Media Foundation's H.264/H.265 decoder (which uses DXVA on modern Windows) on the raw stream pulled from the GDK. The YV12 → BGRA conversion can also stay on the GPU as a colour-conversion shader.

**Gain.** Big — depending on the GPU, ~5–10× cheaper decode CPU-wise. And the decoded surface stays on the GPU end-to-end if we also do #4.1.

**Cost.** We need access to the raw stream **before** the GDK decompresses it. The current C# binding does decompress as one step (`decompress` returns YV12 directly). Pulling out the pre-decompress packets requires a deeper GDK API call than we currently use — which may or may not be exposed.

If the GDK doesn't expose this, falling back to "subscribe to the same NVR with our own RTSP/RTP client" is technically possible (it's a known protocol) but loses the GDK's session management, discovery, fish-eye dewarp, and multiple stream-profile features that we rely on today.

**Realistic gain:** large for CPU; effort gated by what the GDK lets us extract. **Effort:** weeks. **Risk:** medium-to-high.

---

## 5. Decision matrix

|   | Gain | Effort | Risk | Verdict |
|---|---|---|---|---|
| 4.1 — `D3DImage` for tile surfaces | Medium | Medium | Medium | Worth profiling first; second-tier candidate |
| 4.2 — Zero-copy GDK → D3D11 | Very high | High (depends on GDK) | High | **Ask IDIS first**: does the GDK expose D3D11 textures? |
| 4.3 — Custom HLSL blur | Small | Low–Medium | Low | Skip unless profiling demands it |
| 4.4 — Direct2D overlay | Small-Medium | Medium | Medium | Skip; not the bottleneck |
| 4.5 — MediaFoundation HW decode | High | High (depends on GDK) | Medium-High | **Ask IDIS first** — same gate as 4.2 |

---

## 6. What we should do *before* committing to any of this

**Measure first.** We have not actually profiled the render pipeline on production hardware. Every gain estimate above is an educated guess; we could be solving the wrong problem.

Concrete next steps in order:

1. **Run Windows Performance Recorder + Analyzer** with the GPU activity provider enabled on a representative workload (e.g., 4×4 grid on 4K, all AI on). Look at: UI-thread time per frame, GPU queue depth, memory bandwidth. Find the actual bottleneck.
2. **Frame-time histogram** in Display itself: instrument `RenderFrame` and the GDK callback with `Stopwatch` for two minutes, log to bootstrap-style file. Cheap, ships in a Debug build, would tell us within an hour where the time goes.
3. **Ask IDIS technical support** whether the GDK can deliver D3D11 textures or expose the pre-decompress packet stream. This single answer determines whether 4.2 and 4.5 are even on the table.
4. **Only then** decide which of 4.1–4.5 to invest in. Profile data + GDK answer narrows the choice from five to typically one.

---

## 7. A realistic recommendation

If we have to give one answer today, without the profiling above:

- **DirectX is not a silver bullet.** We are already on DirectX for the bit that takes 80% of the per-frame budget (AI inference). The other 20% is split across the GDK's CPU decoder and a CPU→GPU upload that *could* be replaced with D3DImage. The ceiling on what a DirectX-based render rewrite can buy us is therefore at most 20% of total per-frame budget — and even that requires #4.2 / #4.5 to make it worthwhile.
- **The v2.0.1 BlurEffect fix and the v2.0.2 ArrayPool pass already addressed the two known concrete performance complaints** (mass-mask sluggishness, GC pressure). We have no current field reports of sustained performance issues at the documented hardware baseline (Intel i5 12th-gen, RTX 4060, 4×4 grid).
- **The biggest leverage available without a major rewrite is on the IDIS GDK side**: hardware decode + zero-copy textures. That requires a conversation with IDIS, not a code change on our end.

If Joris wants to make the case for a port, the productive shape of that case is:
- A concrete profiling trace showing the bottleneck
- Plus a concrete answer from IDIS on whether the GDK supports D3D11 interop

Without both of those, a DirectX rewrite is mostly hope and significant new complexity.

---

## 8. Open questions to discuss

1. **Joris's specific suggestion** — which of the five variants in §1 did he actually mean? "DirectX" alone is too broad to commit to.
2. **GDK API** — is there a contact at IDIS who can answer the texture-interop question? The lead has the relationship.
3. **Profiling data** — can we get one customer's real workload profiled (with permission) before deciding? Synthetic benchmarks on the dev machine will under-estimate the 4K-grid cost.
4. **Acceptance criteria** — what does "much faster" mean for our customers? Lower CPU? Higher frame rate? Lower power draw on the kiosk PC? Each answer points at a different change.

Once we have answers to (1) and (2) we can take a focused decision.

---

*This is a discussion document, not a plan. No code changes have been made. Comments / corrections from Joris and the team are welcome before any work is scheduled.*
