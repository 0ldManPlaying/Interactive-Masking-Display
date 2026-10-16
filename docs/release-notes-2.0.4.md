# InteractiveMask v2.0.4 — Release Notes

**Release date:** 2026-10-16  
**Previous release:** v2.0.3

---

## TL;DR

v2.0.4 migrates InteractiveMask from IDIS GDK **6.6.1.07** to GDK **7.0.0.13** (full bump: managed C# sampler bindings and native `G2ClientGDK.dll` from the same 7.0.0 package). Empty stubs cover the two new `g2watch_listener` control-session callbacks required to compile against the 7.0.0 interface.

---

## What's new

### IDIS GDK 7.0.0.13

- Default `GdkPath` in `Directory.Build.props` now points at `E:\ISS GDK\GDK-RAS-x64-7.0.0.12-260122\7.0.0` (override remains via `IDIS_GDK_PATH`).
- Rebuild against 7.0.0 headers/bindings is required: `G2LAYOUT` array sizes grew from 64 to 256; mixing a 6.6.1 build with the 7.0.0 DLL is unsafe.
- `G2FishEye.dll` is no longer part of the GDK 7.0.0 package (fish-eye control lives in the core DLL). The csproj still copies it when present for older local packages only.
- Runtime set copied next to the Display EXE: `G2ClientGDK.dll`, `G2LibavMT.dll`, `SDL2.dll`.

### Listener interface

- Stub implementations of `on_g2watch_receive_control_session_expired` and `on_g2watch_receive_response_control_session_renew` in:
  - `NvrSession`
  - `NvrCameraNameFetcher`
  - `tools/PoC.GdkConnect`
- OTP authentication and control-session renew flows are **not** enabled in this release; stubs satisfy the interface only.

---

## Documentation

- README, developer guide, use-case brief, and product-facing notes updated from GDK 6.6.1 / G2FishEye-required wording to GDK 7.0.0.

---

## Upgrade notes

1. Install or point `IDIS_GDK_PATH` at the GDK 7.0.0 package root (folder that contains `bin\` and `sampler\csharp\`).
2. Rebuild Display + WebHost together (`dotnet build InteractiveMask.sln -c Release -p:Platform=x64`).
3. Redeploy both assemblies and the new native DLLs from the 7.0.0 package — do not mix 6.6.1 natives with a 7.0.0-built app.
4. No application config migration beyond prior v2.0.x releases.

### Hardware / field checks (operator)

After deploy, verify on real NVRs:

- HEVC (H.265) live tiles decode correctly
- Multi-NVR layouts still connect and show video
- Disconnect / reconnect recovery
- Camera-name sync from device status
