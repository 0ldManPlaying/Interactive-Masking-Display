# InteractiveMask

**Version 2.0.0**

## Demo

<video src="https://github.com/0ldManPlaying/Interactive-Masking-Display/releases/download/v1.1.1/IPM.mp4" controls width="720" muted></video>

> If the player doesn't appear in your viewer, [download the demo here](https://github.com/0ldManPlaying/Interactive-Masking-Display/releases/download/v1.1.1/IPM.mp4) (228 MB, MP4).

Windowless C# / .NET 9 / WPF kiosk application that connects to one or more **IDIS NVR** systems and displays a live video grid (1×1 up to 5×5) with per-tile **privacy blur masks** that can be toggled with a single click. Designed for environments where continuous oversight and privacy must coexist — healthcare facilities, office spaces, receptions, hotels, schools, and other sites with stricter privacy defaults.

A companion ASP.NET Core WebHost provides the same functionality over a browser, communicating with the kiosk over a local named-pipe IPC channel.

## What's new in 2.0.0

- **AI-driven privacy masking** — YOLO26s-seg segmentation runs locally on the GPU via DirectML and applies privacy masks per detected object instead of the whole tile. Three categories: **Person** (red), **TwoWheeler** (orange), **Vehicle** (blue).
- **Per-camera AI configuration** — enable / disable per camera, pick categories, tune confidence threshold (15–70 %) with hysteresis to remove flicker on small / distant objects, set mask-padding (0–50 %).
- **Region of Interest polygon** — draw a polygon on a live snapshot to restrict detection to a specific area; detections outside the polygon are not masked. Drag-to-move on existing vertices.
- **Mask-style toggle** — choose between **Color-coded silhouette** (per-class colour clipped by the segmentation mask) and **Source blur** (classic CCTV look — the camera area itself blurred).
- **Mask opacity slider** — 20–100 % per camera so operators can let background context show through when needed.
- **Redesigned AI-settings dialog** in the app's card-style language, fully localized NL / EN / DE / FR / ES.
- **About tab → AI runtime info** — live model name, execution provider, average inference latency, frame counter.
- **Audit-log coverage** for the full AI lifecycle (model load, per-camera enable, ROI edits, threshold changes).
- **ONNX Runtime DirectML** upgraded 1.20.1 → 1.24.4.
- **Centralized inference coordinator** — single background worker per camera; no more native crashes under concurrent inference attempts.

See [`docs/release-notes-2.0.0.md`](docs/release-notes-2.0.0.md) for the full list, breaking-change notes, acceptance tests, and the upgrade path from v1.3.x.

The v1.3.0 release notes remain at [`docs/release-notes-1.3.0.md`](docs/release-notes-1.3.0.md) for reference.

## Features

- **Live video grid** — 1×1 / 2×2 / 3×3 / 4×4 / 5×5 layouts driven by the IDIS GDK 6.6.1 (H.264 + HEVC), default stream picked per tile (high / normal / low).
- **Multi-NVR** — single installation can connect to multiple IDIS NVRs simultaneously; each tile picks its own NVR + camera + stream.
- **Privacy blur** — Gaussian blur attached only when masked (perf-friendly). Optional auto-unmask / auto-rollback timer per tile.
- **Two privacy modes** — *OversightDefault* (visible by default, tap to apply privacy) or *PrivacyDefault* (blurred by default, tap to reveal briefly).
- **Mass mask / unmask** — long-press 500 ms anywhere on the kiosk to apply or release privacy across every tile. Useful when the operator briefly leaves an unattended station.
- **Two authentication modes** — session PIN (single secret per session) **or** Windows Active Directory (per-user identity in audit log).
- **Kiosk lock** — low-level keyboard hook blocks Win, Alt+F4 / Tab / Esc, Ctrl+Esc; topmost window.
- **Five-language UI** with **live language switching** — NL, EN, DE, FR, ES, no restart required.
- **First-run language picker** — first launch shows a small modal asking which language to use, pre-selected on the Windows UI culture.
- **Encrypted state** — admin PIN, NVR password (per NVR), web cert password, and session PIN all DPAPI-encrypted (LocalMachine scope).
- **Audit log** — NDJSON event stream with mask on/off, mass actions, PIN failures/lockouts, AD authentication, NVR connect/disconnect, optional syslog forwarding to a SIEM.
- **Browser remote control** — ASP.NET Core WebHost (HTTP/HTTPS), same security policy as the desktop click flow.
- **Per-tile overlay bar** — status indicator + NVR-side camera title (left) + operator-typed custom label + privacy badge (right). Each label independently toggleable.
- **Signed WiX MSI installer** — registers the WebHost as a Windows service, opens firewall rules, optional EV-Authenticode signing.
- **In-app interactive manual** — 8 chapters in NL + EN with inline graphics.

## Architecture

```
┌──────────────────────────┐         ┌──────────────────────────┐
│  InteractiveMask.Display │         │ InteractiveMask.WebHost  │
│  (WPF kiosk, .NET 9)     │ ◄─IPC─► │  (ASP.NET Core, Kestrel) │
│  - GDK live decode       │  named  │  - Razor Pages UI        │
│  - Blur + click handling │  pipe   │  - State mirror          │
│  - Audit log writer      │         │  - Remote toggle         │
└──────────────────────────┘         └──────────────────────────┘
            │
            ▼   (one or more NVRs, multi-recorder support)
┌──────────────────────────┐
│   IDIS NVR (G2 Client)   │
└──────────────────────────┘
```

| Project | Description |
|---|---|
| `InteractiveMask.Display` | WPF kiosk app. Owns the video grid, mask state, audit log, setup window, in-app help |
| `InteractiveMask.WebHost` | ASP.NET Core service for browser remote control |
| `InteractiveMask.Ipc` | Shared named-pipe IPC: bidirectional, length-prefixed JSON, request/response correlation |
| `InteractiveMask.Gdk` | C# wrapper around the IDIS GDK 6.6.1 native libraries |
| `InteractiveMask.Setup` | WiX 5 MSI installer |

## Build

Requirements:
- Windows 10/11 x64
- .NET 9 SDK
- IDIS GDK 6.6.1 native runtime (`G2ClientGDK.dll`, `G2LibavMT.dll`, `G2FishEye.dll`, `SDL2.dll`) — copied alongside `InteractiveMask.Display.exe`
- WiX 5 SDK (only for installer build)

```bash
dotnet build InteractiveMask.sln -c Release -p:Platform=x64
```

To produce an MSI:

```powershell
.\build-installer.ps1 -Version 2.0.0
```

For a signed MSI (Authenticode — e.g. SafeNet EV-token cert in `CurrentUser\My`):

```powershell
.\build-installer.ps1 -Version 2.0.0 -Sign -CertSubject 'IDIS Nederland BV'
# or by thumbprint:
.\build-installer.ps1 -Version 2.0.0 -Sign -CertThumbprint <40-hex>
```

`-Sign` first signs every `InteractiveMask.*.exe` / `.dll` (so the cab embedded in the MSI contains signed binaries) and then signs the MSI itself. SHA-256 file digest + SHA-256 timestamp digest. The default timestamp server is DigiCert; override via `-TimestampUrl`.

### Unattended MSI deployment

For SCCM / Intune / mass-rollout scenarios, pre-seed the UI language so end-users don't see the first-run picker:

```
msiexec /i InteractiveMask-2.0.0.msi /qn INTERACTIVEMASK_LANGUAGE=de
```

The value is written to `HKLM\Software\InteractiveMask\InitialLanguage`. App.OnStartup picks it up on first run and skips the picker. Supported codes: `nl`, `en`, `de`, `fr`, `es`. Without the property the registry stays untouched and the first-run picker appears as usual.

## Runtime layout

| Path | Purpose |
|---|---|
| `%PROGRAMDATA%\InteractiveMask\admin.dat` | DPAPI-encrypted admin PIN |
| `%PROGRAMDATA%\InteractiveMask\config.json` | NVR list, cameras, grid, privacy + web settings (passwords DPAPI-encrypted) |
| `%PROGRAMDATA%\InteractiveMask\webhost.pfx` | TLS certificate for the WebHost (auto-generated on first run if missing) |
| `%PROGRAMDATA%\InteractiveMask\audit.log` | NDJSON audit event stream |
| `%LOCALAPPDATA%\InteractiveMask\state.json` | Session-PIN cache (DPAPI-encrypted) |
| `HKLM\Software\InteractiveMask\InitialLanguage` | Optional MSI-seeded language for first run |

## Usage

1. **First run** — the kiosk opens with a language picker. Pick a language, then right-click → *Setup* and set an **admin PIN**.
2. **Setup wizard** — configure one or more NVRs (IP / port / credentials), bind cameras to grid slots, choose grid size, choose authentication mode (PIN / AD / off), pick the privacy mode (oversight / privacy-default), enable / disable the WebHost, and configure audit-log retention.
3. **Normal operation** — click any tile to toggle its privacy blur. Long-press anywhere on the screen for 500 ms to mass-mask or mass-unmask. Auto-unmask / auto-rollback timers are configurable per tile.
4. **Removing a mask** — prompts for PIN (or Windows credentials in AD mode), unless authentication is fully disabled.
5. **Pull NVR titles** — Setup → Cameras → *Pull names from NVR* fetches the camera titles configured on the recorder side and fills the read-only NVR-title column. Operator labels are kept untouched.
6. **Apply / Save** — *Apply* pushes changes to the running kiosk without closing Setup; *Save* does the same and closes.
7. **Kiosk mode** — when enabled, common task-switching key chords are blocked while the window is in focus.

## Security model

- Authentication is required to **remove** a mask (privacy-first: applying blur is always free).
- PIN mode: single per-session secret. Captured on first mask, cleared when no masks remain.
- AD mode: each unmask requires Windows credentials, and the user identity is written into the audit log (`source: user:<sam>`).
- Mass-unmask via long-press is gated by the same auth as a per-tile unmask, plus a *state-guard* that refuses to lift privacy when individual masks are active (prevents accidental privacy breach across an unattended post).
- Lockout: configurable PIN-failure threshold and lockout duration.
- All persisted secrets are DPAPI-encrypted at the LocalMachine scope, so the data never leaves the host.
- MSI + every `InteractiveMask.*.exe` / `.dll` are EV-Authenticode signed (Sectigo Public Code Signing CA R36, IDIS Nederland BV) so SmartScreen verifies the publisher.

## Development notes

- The IDIS GDK is **not thread-safe**: `g2decoder.picture_scale` calls are serialized with a process-wide lock.
- WPF rendering uses `WriteableBitmap` (Bgra32) updated from the GDK callback thread via `Dispatcher.BeginInvoke`.
- HEVC requires the V3 decoder + `g2_color_convert_yv12_to_rgb32`; V2 + RGB32 returns `DECODER_NOT_READY`.
- Live-apply: trivial settings (language, labels, privacy timers, AD toggle, tile-overlay toggles, mass-unmask confirm) are wired through `Func<>` providers and apply without restart. Structural changes (NVR connection, camera bindings, grid size, **privacy mode flip**) prompt for restart.
- Multi-NVR: one `NvrSession` per recorder, each owning its own `g2watch`. Camera-name sync is routed through the live session (`request_device_status` → `on_g2watch_receive_device_status`) so we never open a second concurrent login on the same NVR.
- Per-tile overlay bar uses a `Grid` with two `StackPanel`s (left / right) so the NVR title and operator label can be toggled independently and each align to their own end of the bar.
- Implicit `RadioButton` / `CheckBox` styles in `SetupWindow.xaml` set `Foreground` so default `SystemColors.ControlText` (black) doesn't render unreadable on the dark Setup chrome. The full `ComboBox` `ControlTemplate` override is required for the same reason — setter-only styles do not retheme the WPF default ComboBox visuals.

## License

Proprietary. All rights reserved.
