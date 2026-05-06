# InteractiveMask

**Version 1.0.3**

Windowless C# / .NET 9 / WPF kiosk application that connects to an IDIS NVR and displays a live video grid (up to 16 cameras) with per-tile **privacy blur masks** that can be toggled with a single click. Designed for healthcare facilities (Dutch *zorginstellingen*) where caregivers need to instantly hide a resident's room from on-screen view while still being able to verify the camera is working.

A companion ASP.NET Core WebHost provides the same functionality over a browser, communicating with the kiosk over a local named-pipe IPC channel.

## Features

- **Live video grid** — 2×2 / 3×3 / 4×4 layouts driven by the IDIS GDK 6.6.1 (H.264 + HEVC), stream-2 (low-res) by default for performance
- **Privacy blur** — Gaussian blur attached only when masked (perf-friendly). Optional auto-unmask timer per tile
- **Two authentication modes** — session PIN (single secret per session) **or** Windows Active Directory (per-caregiver identity in audit log)
- **Kiosk lock** — low-level keyboard hook blocks Win, Alt+F4/Tab/Esc, Ctrl+Esc; topmost window
- **Full NL/EN i18n** with **live language switching** — no restart required
- **Encrypted state** — admin PIN, NVR password, web cert password, and session PIN all DPAPI-encrypted (LocalMachine scope)
- **Audit log** — NDJSON event stream with mask on/off, PIN failures/lockouts, AD authentication
- **Browser remote control** — ASP.NET Core WebHost (HTTP/HTTPS), same security policy as the desktop click flow
- **WiX MSI installer** — registers the WebHost as a Windows service and adds the necessary firewall rules
- **In-app interactive manual** — 8 chapters NL+EN with inline graphics

## Architecture

```
┌──────────────────────────┐         ┌──────────────────────────┐
│  InteractiveMask.Display │         │ InteractiveMask.WebHost  │
│  (WPF kiosk, .NET 9)     │ ◄─IPC─► │  (ASP.NET Core, Kestrel) │
│  - GDK live decode       │  named  │  - Razor Pages UI        │
│  - Blur + click handling │  pipe   │  - State mirror          │
│  - Audit log writer      │         │  - Remote toggle          │
└──────────────────────────┘         └──────────────────────────┘
            │
            ▼
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
.\build-installer.ps1
```

## Runtime layout

| Path | Purpose |
|---|---|
| `%PROGRAMDATA%\InteractiveMask\admin.dat` | DPAPI-encrypted admin PIN |
| `%PROGRAMDATA%\InteractiveMask\config.json` | NVR connection, cameras, grid, web settings (passwords DPAPI-encrypted) |
| `%PROGRAMDATA%\InteractiveMask\webhost.pfx` | TLS certificate for the WebHost (auto-generated on first run if missing) |
| `%PROGRAMDATA%\InteractiveMask\audit.log` | NDJSON audit event stream |
| `%LOCALAPPDATA%\InteractiveMask\state.json` | Session-PIN cache (DPAPI-encrypted) |

## Usage

1. **First run** — the kiosk opens to a "no config" screen. Right-click → *Setup*. The first time, you set an **admin PIN**.
2. **Setup wizard** — configure the NVR (IP / port / credentials), bind cameras to grid slots, choose grid size, choose authentication mode (PIN / AD / off), enable/disable the WebHost, and set audit-log retention.
3. **Normal operation** — click any tile to toggle its privacy blur. Auto-unmask timers are configurable per tile.
4. **Removing a mask** — prompts for PIN (or Windows credentials in AD mode), unless authentication is fully disabled.
5. **Kiosk mode** — when enabled, common task-switching key chords are blocked while the window is in focus.

## Security model

- Authentication is required to **remove** a mask (privacy-first: applying blur is always free).
- PIN mode: single per-session secret. Captured on first mask, cleared when no masks remain.
- AD mode: each unmask requires Windows credentials, and the user identity is written into the audit log (`source: user:<sam>`).
- Lockout: configurable PIN-failure threshold and lockout duration.
- All persisted secrets are DPAPI-encrypted at the LocalMachine scope, so the data never leaves the host.

## Development notes

- The IDIS GDK is **not thread-safe**: `g2decoder.picture_scale` calls are serialized with a process-wide lock.
- WPF rendering uses `WriteableBitmap` (Bgra32) updated from the GDK callback thread via `Dispatcher.BeginInvoke`.
- HEVC requires the V3 decoder + `g2_color_convert_yv12_to_rgb32`; V2 + RGB32 returns `DECODER_NOT_READY`.
- Live-apply: trivial settings (language, labels, privacy timers, AD toggle) are wired through `Func<>` providers and apply without restart. Structural changes (NVR connection, camera bindings, grid size) prompt for restart.

## License

Proprietary. All rights reserved.
