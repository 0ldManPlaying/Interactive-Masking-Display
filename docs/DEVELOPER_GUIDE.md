# InteractiveMask — Developer onboarding guide

**Audience:** a developer joining the InteractiveMask project for the first time.
**Goal:** get the code running on your machine, understand the layout, build a signed installer, and contribute changes without breaking anything important.

This guide assumes you already know C# / .NET / WPF basics. It does *not* assume you know IDIS GDK, ONNX Runtime, or the project's conventions — those are explained below.

---

## 1. What the app is

A Windows kiosk that connects to one or more **IDIS NVRs** and shows their live camera feeds in a configurable grid (1×1 up to 5×5). Operators can apply privacy masks — manually per tile, all-tiles-at-once via a long-press, or **automatically per detected object** using a local YOLO model on the GPU (v2.0 feature).

Two processes ship:

- **`InteractiveMask.Display`** — the WPF kiosk window. Owns the GDK decode pipeline, AI inference, click handling, audit log, Setup window, in-app help. Runs as the logged-in user.
- **`InteractiveMask.WebHost`** — an ASP.NET Core service for browser-based remote control. Installed as the Windows service `InteractiveMaskWebHost`, talks to Display over a named pipe.

A signed MSI installs both. Configuration lives in `%PROGRAMDATA%\InteractiveMask\`.

For the full feature inventory + use cases see [`release-notes-2.0.0.md`](release-notes-2.0.0.md) and the user-facing emails in `docs/email-collegas-v2.0.0.md` (NL) / `docs/email-colleagues-v2.0.0.md` (EN).

---

## 2. Repository layout

```
InteractiveMask/
├── src/
│   ├── InteractiveMask.Display/      WPF kiosk app (main entry point)
│   ├── InteractiveMask.WebHost/      ASP.NET Core remote-control service
│   ├── InteractiveMask.Ipc/          Named-pipe IPC primitives, shared between Display and WebHost
│   ├── InteractiveMask.Gdk/          C# wrapper around the native IDIS GDK 6.6.1 DLLs
│   ├── InteractiveMask.Detection/    AI detector contracts + ONNX Runtime implementation
│   ├── InteractiveMask.Hardware/     Host-capability probe (GPU / RAM / CPU) + benchmark runner
│   └── InteractiveMask.Setup/        WiX 5 MSI installer project
├── docs/                             Roadmap, architecture, release notes, this file
├── images/                           Branding assets (logos referenced from WPF)
├── tools/
│   └── sign-build.ps1                Authenticode signing wrapper around signtool.exe
├── build-installer.ps1               Top-level installer build (publishes both apps + signs + builds MSI)
├── InteractiveMask.sln               Solution file
└── Directory.Build.props             Common csproj properties (TargetFramework, Platform=x64, etc.)
```

**Per-project responsibilities (quick reference):**

| Project | Purpose | Heavy hitters |
|---|---|---|
| `Display` | The kiosk window itself | `MainWindow.xaml.cs` (everything), `MaskController.cs` (mask + reveal + auth), `TileViewModel.cs` (per-tile state + render-binding), `ConfigService.cs` (persisted config) |
| `WebHost` | Browser remote control | `Program.cs` (DI + Kestrel), `WebAccessAuth.cs` (session store + middleware) |
| `Ipc` | Display↔WebHost wire format | `IpcServer.cs`, `IpcClient.cs`, length-prefixed JSON |
| `Gdk` | IDIS native bindings | `NvrSession.cs` (one session per NVR; connect / reconnect / per-camera channels), `CameraTile.cs` (per-camera frame buffer + decode callbacks) |
| `Detection` | AI pipeline | `IObjectDetector.cs` (contract), `OnnxLocalDetector.cs` (in-process ORT), `InferenceCoordinator.cs` (single-worker ORT, slot-replacement), `DegradationController.cs` (F3 adaptive load) |
| `Hardware` | Capability probe | `HostCapabilityProfile.cs`, `BenchmarkRunner.cs` |
| `Setup` | MSI build | `Package.wxs`, `License.rtf` |

---

## 3. Prerequisites for the dev machine

- **Windows 10 22H2 or 11 23H2 / 24H2, x64.** The app is Windows-only because WPF is Windows-only.
- **.NET 9 SDK** (current `Directory.Build.props` pins `net9.0-windows`). Get it from <https://dotnet.microsoft.com/download/dotnet/9.0>.
- **Visual Studio 2022 17.10+** or **VS Code with the C# Dev Kit**. Either works; the team uses both.
- **IDIS GDK 6.6.1** native DLLs in your local path. The native runtime is not on NuGet — ask the lead for the binary drop and copy `G2ClientGDK.dll`, `G2LibavMT.dll`, `G2FishEye.dll`, `SDL2.dll` alongside `InteractiveMask.Display.exe` after a build. Without them the app starts but every tile shows "No video".
- **A DirectML-capable GPU** for AI work — NVIDIA RTX, AMD 700-series, or Intel Arc with a driver from 2020 or newer. Not strictly required to compile or to test v1.x features; AI will silently fall back to v1.x-style manual masking on hosts without a suitable GPU.
- **WiX 5 SDK** — needed only when building the MSI. NuGet fetches it automatically on first installer build.
- **Windows 10/11 SDK with signtool** — needed only for signed builds. Comes with VS, or stand-alone.
- **An NVR to point at.** Either a real IDIS NVR on the LAN or test credentials from the lead. Without one you can compile and start the app but won't see any live video.
- **A code-signing cert (optional)** — only needed if you'll build production MSIs. Lives on a SafeNet token; the lead has it.

---

## 4. Get the code, build, run

```powershell
git clone https://github.com/0ldManPlaying/Interactive-Masking-Display.git
cd Interactive-Masking-Display
```

Branching convention: feature work goes on `main` directly with conventional commits (no PR gate today, but use `feat:`, `fix:`, `chore:`, `docs:`, `perf:` prefixes — the existing log is the style reference). Tags are reserved for releases (`vX.Y.Z`).

### Build everything (Debug)

```powershell
dotnet build src/InteractiveMask.Display/InteractiveMask.Display.csproj -c Debug -p:Platform=x64
```

**Important:** always pass `-p:Platform=x64`. Output without it lands under `bin/Debug/` (no platform suffix) instead of `bin/x64/Debug/`, and your shortcut probably points at the `bin/x64/` tree — we've burned hours on this confusion before. The csproj defines `x64` explicitly; `AnyCPU` builds are not useful.

Output: `src/InteractiveMask.Display/bin/x64/Debug/net9.0-windows/win-x64/InteractiveMask.Display.exe`.

### Run from the IDE

Set `InteractiveMask.Display` as the startup project, F5. On first launch:

1. A small language picker appears (NL / EN / DE / FR / ES). Pick one.
2. The app asks you to set an admin PIN (4 digits) — this guards Setup and the kiosk-exit path.
3. The grid loads. If no NVR is configured, it shows "Configuration loaded — open Setup to add an NVR".
4. Open **Setup → NVR**, add an IDIS NVR with its IP + credentials.
5. Switch to **Setup → Cameras**, add a row, point at a camera index on the NVR you just added.
6. **Save** or **Apply** — the live video should appear within a few seconds.

### Run the WebHost separately for browser-control work

```powershell
dotnet run --project src/InteractiveMask.WebHost
```

It binds to `http://localhost:8080` by default (overridable in `appsettings.json`). Browser to that URL while Display is also running — the named-pipe IPC connects automatically.

---

## 5. Where things actually live (code map)

A new developer's most-frequent question is "where do I find X". Here's the cheat sheet:

| You want to change… | Look here |
|---|---|
| Tile rendering / per-tile XAML | `src/InteractiveMask.Display/MainWindow.xaml` |
| Per-tile state (IsMasked, Detections, AI-reveal, …) | `src/InteractiveMask.Display/TileViewModel.cs` |
| Mask click flow + PIN/AD auth + audit | `src/InteractiveMask.Display/MaskController.cs` |
| Setup window UI | `src/InteractiveMask.Display/SetupWindow.xaml` + `SetupWindow.xaml.cs` |
| Per-camera AI settings dialog | `src/InteractiveMask.Display/CameraAiSettingsDialog.xaml` |
| ROI polygon editor | `src/InteractiveMask.Display/RoiEditorDialog.xaml` |
| Persisted config schema + DPAPI-encrypted fields | `src/InteractiveMask.Display/ConfigService.cs` + `NvrSettings.cs` |
| Live-apply path (Setup → Apply without restart) | `MainWindow.xaml.cs::ApplyCameraChangesLive` + `ReconcileNvrFleet` |
| AI detector contract + concrete impl | `src/InteractiveMask.Detection/IObjectDetector.cs`, `OnnxLocalDetector.cs` |
| ONNX inference + YOLO26s-seg decoder | `src/InteractiveMask.Detection/InferenceCoordinator.cs` |
| Adaptive load manager (F3) | `src/InteractiveMask.Detection/DegradationController.cs` |
| Localised strings (NL/EN/DE/FR/ES) | `src/InteractiveMask.Display/Strings.cs` |
| In-app help content | `src/InteractiveMask.Display/HelpContent.cs` + `HelpWindow.xaml` |
| GDK lifetime / decode callbacks | `src/InteractiveMask.Gdk/NvrSession.cs`, `CameraTile.cs` |
| IPC wire format | `src/InteractiveMask.Ipc/IpcServer.cs`, `IpcClient.cs` |
| Audit log writer | `src/InteractiveMask.Display/AuditLog.cs` |
| Startup diagnostic log (v2.0.1+) | `src/InteractiveMask.Display/BootstrapLog.cs` |
| MSI install steps + firewall rule + service install | `src/InteractiveMask.Setup/Package.wxs` |
| Installer build orchestration | `build-installer.ps1` |
| Authenticode signing | `tools/sign-build.ps1` |
| Roadmap (what's planned, what's done) | `docs/roadmap.md` |
| AI-pipeline architecture (released as v1.0) | `docs/architecture-v2-ai.md` |
| Per-release notes | `docs/release-notes-X.Y.Z.md` + `.docx` |
| Build / distribution notes | `docs/buildnote-2.0.0.md` |

---

## 6. Conventions & things to know

### Code style

- **Comments explain "why", not "what".** The code can tell what it does; comments should make it clear why something is the way it is — especially trade-offs, race conditions averted, and design decisions worth preserving. Look at `MaskController.cs` for the house style.
- **Long XML doc-comments** on public types/methods that have non-obvious contracts. Quick `// inline` notes inside methods are fine.
- **No emoji in source code or commit messages.** They're fine in user-facing UI strings if the design calls for it; we currently don't use any.
- **Naming**: `PascalCase` for everything public, `_camelCase` for private fields, `kCamelCase` not used.
- **`async`/`await` everywhere except WPF event handlers**, which use `async void` carefully.

### Commit messages

Conventional-commit style: `prefix(scope): subject` on the first line under 72 chars, then a blank line, then a paragraph explaining context. See `git log --oneline` for the canonical examples.

Common prefixes: `feat`, `fix`, `perf`, `docs`, `chore`, `refactor`. Scope is optional; use it when the commit is clearly about one subsystem (`feat(reveal):`, `perf(memory):`, `fix(ai-dialog):`).

Always end the commit body with the `Co-Authored-By:` line — see the existing commits for the format.

### Things that will bite you

These have all caused real bugs; the fixes are documented in the source but worth knowing up front:

- **Never call `_session.Run()` from multiple threads concurrently.** ORT crashes hard. The `InferenceCoordinator` exists for exactly this — *all* inference goes through its single worker, with slot-replacement (newer frames overwrite older pending ones rather than queueing). If you add a new caller, route it through `Submit()`.
- **WPF triggers reject `{TemplateBinding}` and `{Binding}` as `Setter.Value` inside a `Trigger`.** They produce `"Expression type is not a valid Style value"` at parse time. Use literal values, `{StaticResource}`, or `{DynamicResource}` — or restructure the template to put the dynamic binding on a child element whose static property the trigger toggles (typical example: `Opacity` flip on a pre-coloured `<Border>`).
- **`Path.Combine` will resolve to `BootstrapLog.Path` (the property) when you `using System.IO` inside `BootstrapLog.cs`.** Use `System.IO.Path.Combine(...)` explicitly there.
- **Always build with `-p:Platform=x64`.** See section 4 above.
- **Don't write to `AppContext.BaseDirectory`** at runtime — that's `C:\Program Files\InteractiveMask\Display\` on a real install, and default users can't write there. Use `%PROGRAMDATA%\InteractiveMask\` (writable for any local user) for logs and config.
- **`StartupMode.OnLastWindowClose` (WPF default) can shut down the app when the first-run language picker closes**, because `MainWindow` doesn't exist yet at that point. The picker is wrapped in `ShutdownMode.OnExplicitShutdown` for the duration — see `App.OnStartup`.
- **`File.AppendAllText` on the UI thread will stall** when AV scanners are slow. The audit log uses a persistent `StreamWriter` (`AuditLog.cs`). Don't revert to AppendAllText.
- **Per-frame allocations on the GDK callback thread are very expensive** (16 tiles × 25 fps = 400 calls/sec). Pool everything in the hot path — see `BitmapFrameRef` and the `ArrayPool` usage in `InferenceCoordinator.cs` and `TileViewModel.OnFrameDecoded`.
- **Localisation lookups read `Strings.Instance.Current.X`** via the `{local:Loc X}` XAML markup extension. Adding a new string requires (1) a property on `StringsTable` in `Strings.cs`, (2) a translation in each of the 5 language blocks in the same file, (3) the `{local:Loc X}` reference in XAML. Skipping any one of the five language blocks leaves an empty string for that locale — easy to miss.

### Diagnostic logs

| File | Purpose | Path |
|---|---|---|
| `bootstrap.log` | Startup trace — every phase from `App.OnStartup.begin` through `MainWindow.OnLoaded.before-PromptForNewPin`. Truncated on each launch. | `%PROGRAMDATA%\InteractiveMask\bootstrap.log` |
| `crash.log` | Unhandled exception stack traces, append-only. | `%PROGRAMDATA%\InteractiveMask\crash.log` |
| `audit.log` | NDJSON event stream — every mask on/off, mass action, PIN attempt, NVR connect/disconnect, AI lifecycle event. | `%PROGRAMDATA%\InteractiveMask\audit.log` |

When a customer reports a problem the first thing you ask for is these three files.

---

## 7. AI-pipeline specifics

The detector contract is `IObjectDetector` (in `Detection`). One implementation today: `OnnxLocalDetector` (in-process ONNX Runtime + DirectML). A second is stubbed for the planned Jetson ARM64 sidecar (`JetsonRemoteDetector`, throws `NotImplementedException` for now).

### Model files

- `src/InteractiveMask.Detection/models/yolo26s-seg.onnx` (42 MB) — production
- `src/InteractiveMask.Detection/models/yolo26n-seg.onnx` (11 MB) — lighter fallback

These are checked in. They're copied to the publish output via `<Content>` items in both `InteractiveMask.Detection.csproj` (build output for direct refs) and `InteractiveMask.Display.csproj` (`<Content Include="..\InteractiveMask.Detection\models\*.onnx" Link="models\%(Filename)%(Extension)">` — needed because transitive content flow through `dotnet publish` is unreliable). Don't remove either declaration.

### Output format (YOLO26s-seg)

NMS-free, 300 detection slots per frame. Two output tensors:

- `output0` shape `(1, 300, 38)`: for each slot, `[x1, y1, x2, y2, score, class_id, mask_coeff_0..31]` in 640-input-pixel space.
- `output1` shape `(1, 32, 160, 160)`: 32 prototype masks. Per detection, the 32 coefficients combined with the prototypes (sigmoid + threshold at 0.5) produces a binary alpha mask in proto-space; we resample to source-bbox dimensions.

`InferenceCoordinator.DecodeNmsFree` does all of this. The mask handling lives in `BuildSegmentationMask`.

### Per-camera configuration

Every AI knob is persisted per camera in `CameraSlotSettings` (see `NvrSettings.cs`):

- `AiEnabled` — master toggle (default OFF as of v2.0.2)
- `AiClasses` — set of `ObjectClass` values (Person / TwoWheeler / Vehicle today; Face / LicensePlate reserved)
- `AiConfidencePercent` — 15–70 with hysteresis (stay-threshold = enter/2, IoU≥0.4 frame match)
- `MaskPaddingPercent` — 0–50
- `AiRoiPolygon` — drag-and-place vertices on a live snapshot
- `AiUseSourceBlur` — false = colour-coded silhouette, true = sample-the-camera-and-blur (CCTV look)
- `AiMaskOpacityPercent` — 20–100

All round-trip through `ConfigService.StoredCamera` (the JSON DTO). Add a new field: declare on both, map in `ApplyStored` + `ToStored`, default it sensibly for backward compatibility.

### Adaptive load manager (F3)

`DegradationController` watches rolling p95 inference latency and walks a 3-step ladder (FrameSkip → PerCameraDisable → GlobalFallback) with hysteresis to prevent flapping. The Display project subscribes to `StateChanged` and updates per-tile `IsAiSuspendedByLoad` (drives a small badge on the tile). Read its xml-doc for the constants and design.

---

## 8. Building a signed MSI

```powershell
.\build-installer.ps1 -Version 2.0.X
```

Unsigned. Output: `build\publish\InteractiveMask-2.0.X.msi` (~180 MB self-contained).

For a signed build the lead plugs in the SafeNet EV token first, then:

```powershell
.\build-installer.ps1 -Version 2.0.X -Sign -CertThumbprint '39F90C0C81B45F2FAE47677E623A7F5D79185A19'
```

The thumbprint is the IDIS Nederland BV code-signing cert; lives in `CurrentUser\My` once the SafeNet token is unlocked. The script signs every `InteractiveMask.*.exe` and `.dll` before WiX harvests them, then signs the MSI itself last. SHA-256 file digest + SHA-256 timestamp via DigiCert.

You won't need to do this — only the release manager builds production MSIs. But know that `build-installer.ps1` is idempotent and safe to run for unsigned local testing.

---

## 9. Release flow

When the batch of fixes / features is ready:

1. **Bump versions** in `src/InteractiveMask.Display/InteractiveMask.Display.csproj` and `src/InteractiveMask.WebHost/InteractiveMask.WebHost.csproj` (`Version`, `FileVersion`, `AssemblyVersion` — three matching values).
2. **Commit the bump** with message `vX.Y.Z: bump version`.
3. **Write release notes** as `docs/release-notes-X.Y.Z.md`. For major releases also build the `.docx` via `docs/build-release-notes-X.Y.Z-docx.js` (Node + the global `docx` npm package).
4. **Build signed MSI** via `build-installer.ps1 -Version X.Y.Z -Sign -CertThumbprint <thumb>`.
5. **Verify the MSI** — `Get-AuthenticodeSignature` should report `Valid` + the expected signer.
6. **Tag** with `git tag -a vX.Y.Z -m "vX.Y.Z — <one-line summary>"` and `git push origin vX.Y.Z`.
7. **Create a GitHub Release** via `gh release create vX.Y.Z --title "..." --notes "..." path/to/MSI`.
8. **Distribute** — link the GitHub release in the existing colleague-email templates under `docs/`.

See `docs/buildnote-2.0.0.md` for an example of the per-release artifact.

---

## 10. Useful reference reading (in order of importance)

1. **`docs/roadmap.md`** — what's done, what's planned, what's parked. Look here before starting any non-trivial feature.
2. **`docs/architecture-v2-ai.md`** — the AI pipeline design at architecture level. Status sections call out what's actually shipped versus what's planned.
3. **`docs/release-notes-2.0.0.md`** — full feature inventory of the v2.0 line.
4. **`docs/release-notes-2.0.1.md`** + **`release-notes-2.0.2.md`** (in commit messages / GitHub releases) — patch-level changes.
5. **`README.md`** — the customer-facing overview. Useful when you need to write something user-facing — keep the same voice.

---

## 11. Getting help

- **Support channel**: support@bnl.idisglobal.com (also the public contact in the README).
- **Project-specific questions**: ask the lead developer directly.
- **For the IDIS GDK** specifically: IDIS Co., Ltd. provides the SDK; the lead has the contact for their technical support.

---

## 12. First task suggestions

Good places to make a first small change and get familiar with the build/test loop:

- Pick an item from the `v2.0.x patches → Carry-over polish` table in `docs/roadmap.md` — those are well-scoped and low-risk.
- Add an entry to a localisation block in `Strings.cs` and verify it shows up in the live UI after switching language in Setup.
- Read `MaskController.HandleTileClick` and trace one full click → audit-log flow end-to-end.
- Read `InferenceCoordinator.Submit` + `WorkerLoop` + `ProcessSlot` and trace one frame from `TileViewModel.OnFrameDecoded` through to `ApplyDetections`.

Welcome to the project.
