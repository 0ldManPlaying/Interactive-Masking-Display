# InteractiveMask v1.3.0 — Release Notes

**Release date:** 8 May 2026
**Build:** `InteractiveMask-1.3.0.msi` (109 MB, self-contained)
**Signed by:** IDIS Nederland BV (EV Authenticode, Sectigo Public Code Signing CA R36, SHA-256 + DigiCert timestamp)
**Compatibility:** Windows 10/11 x64, requires IDIS GDK 6.6.1 native runtime alongside the EXE
**Upgrade path:** in-place over v1.2.0 / v1.1.x / v1.0.x — existing `config.json` migrates automatically

---

## Headline features

### 1. Five UI languages (NL / EN / DE / FR / ES)

The application now ships with full translations in five languages, professionally toned for a business audience. A single 226-key `StringsTable` per language drives every UI element, dialog, status message and audit-tab label.

**For the operator:** the existing in-app language switcher (Setup → Administrator → User-interface language) becomes a five-entry dropdown showing each language's native name (Nederlands, English, Deutsch, Français, Español). Switching is live; no restart needed.

**For first-run users:** a small modal language picker now appears when no `config.json` exists yet, pre-selected on the Windows UI culture. Picking a language seeds the runtime session; the choice is persisted at the first Setup save.

**For SCCM / Intune deployments:** an optional MSI command-line property `INTERACTIVEMASK_LANGUAGE` skips the first-run picker entirely:

```
msiexec /i InteractiveMask-1.3.0.msi /qn INTERACTIVEMASK_LANGUAGE=de
```

The value is written to `HKLM\Software\InteractiveMask\InitialLanguage`. App.OnStartup reads it on first run only and applies it directly. Without the property, the registry stays untouched and the picker behaves as usual.

### 2. Long-press mass mask / unmask

Hold the left mouse button anywhere on the kiosk window for 500 ms to mask or unmask **every tile at once**. The direction follows the current state:

- All tiles already masked → mass-unmask (auth required).
- Anything else (mixed or fully visible) → mass-mask (free; privacy-first).

Mass-unmask is gated by a **state guard**: the action only proceeds when every tile is currently masked. A mixed state (some tiles individually privacy-masked by another caregiver) blocks mass-unmask to prevent accidental privacy breach. Authentication for the unmask side follows the existing per-tile policy (PIN or AD); a confirmation dialog before the unmask is opt-in via Setup → Privacy.

Use case: an operator briefly leaves an unattended monitoring station and wants to drop all imagery to privacy in one motion, then restore it on return.

### 3. Privacy-default mode

Setup → Privacy gains a *Default visibility* card with two choices:

- **OversightDefault** (existing v1.2 behaviour). Tiles boot visible. Tap to apply privacy. Removing privacy is auth-aware.
- **PrivacyDefault** (new). Tiles boot blurred. Tap to briefly reveal for verification. An *auto-rollback* timer brings the tile back to blurred. Authentication on each reveal is opt-in via a sub-toggle.

Privacy-default fits sites with stricter privacy requirements: office spaces, receptions, hotels, schools, and healthcare facilities that prefer blurred-by-default monitoring over the classic visible-by-default model.

A mode change is treated as a structural change and prompts for a Display restart so the boot state of every tile and the auto-timer direction stay coherent with the chosen mode.

### 4. Multi-NVR support (carried over from v1.2, refined in v1.3)

A single InteractiveMask installation can connect to multiple IDIS NVRs simultaneously and intermix them on one grid. Setup → NVR holds a list editor; Setup → Cameras lets each tile pick its own NVR + camera + stream. One `NvrSession` per recorder, with independent connect / disconnect / reconnect state. v1.3 adds:

- A modal NVR-edit dialog (separate from the camera list) for clearer add / edit flows.
- Per-NVR connection banner that names the affected recorder when one of several drops.

### 5. NVR camera-name sync

Setup → Cameras gains a *Pull names from NVR* button. Click it and the kiosk asks each connected NVR for its `G2DEVICE_STATUS._camera_desc` array, then fills a new read-only **NVR Title** column with the camera names as configured on the recorder side. The existing operator-typed *Custom Label* column is **never touched** — operators retain full control over what they want each tile to read in the live grid.

Implementation note: the sync routes the request through the **live** Display NVR session via a new `NvrSession.FetchCameraNamesAsync(timeout)` method. Most IDIS NVRs reject a second concurrent login from the same user, so a standalone fetcher would have failed. Reusing the existing authenticated session avoids that conflict entirely.

NVR Title is now a persisted field on `CameraSlotSettings`, written to `config.json` alongside the operator label, slot, NVR ID, camera index and stream. After a Setup save the kiosk shows the recorder-side name from the first frame, no live device-status round-trip needed.

### 6. Tile overlay bar redesigned

The bottom bar of each tile is now a two-cluster grid:

- **Left:** status indicator (live / connecting / disconnected) + NVR-side camera title + status text.
- **Right:** operator-typed custom label + privacy badge.

Both texts toggle independently from Setup → Privacy → *Tile overlay*:

- *Show custom label* (default on)
- *Show NVR title* (default off — existing installs don't suddenly grow a second overlay line; turn it on to see the recorder-side name)

When both are on, NVR title sits left, custom label sits right.

### 7. Drag-and-drop tile reorder

Setup → Cameras → Slot bindings grid grows a grip-handle column at the front. Hold the grip and drag a row up or down to change its slot order; slot numbers are renumbered automatically on drop. The whole reorder runs in-memory and only persists when the operator clicks Apply / Save.

### 8. Apply button

Setup gains a third action button between Cancel and Save: **Apply** validates, persists to `config.json`, and pushes the new settings to the running kiosk via the existing live-apply pipeline — but **leaves the Setup window open**. Operators can iterate on changes (camera labels, privacy toggles, etc.) and watch the live grid react without closing and reopening Setup each round.

### 9. About tab

A new entry in the Setup sidebar shows:

- Product card: runtime version (read via reflection from the assembly), publisher (IDIS Nederland BV), copyright, and a one-paragraph product description.
- Integrations: IDIS Direct IP NVR's, SDL2 (zlib license), Authenticode signing (Sectigo Public Code Signing CA R36).
- Open-source components: .NET 9, ASP.NET Core 9, Microsoft.Extensions.\*, ProtectedData, WiX Toolset 5, Segoe Fluent / MDL2 Assets — each with license and attribution.
- Bug report card: opens the user's default email client with a pre-filled subject (`InteractiveMask vX.Y.Z - bug report`) and runtime / OS info in the body, addressed to `support@bnl.idisglobal.com`. Website link to <https://www.idisglobal.solutions> in the Product card.

### 10. Smaller features

- **5×5 grid** option (25 tiles) added to the existing 1×1 / 2×2 / 3×3 / 4×4 set.
- **IDIS watermark** (10% opacity) rendered in empty grid tiles so the kiosk feels finished even when slots are unbound.
- **Aspect-ratio fix** for non-16:9 cameras. Tile `<Image>` switched from `Stretch=UniformToFill` (cropped) to `Stretch=Uniform` (fit). 4:3, 5:4, 1:1 fish-eye and 21:9 sensors are now shown in full with letterbox / pillarbox bars against the tile background, instead of silently losing edge content.
- **Setup window** grew to 1180 × 1000 px so every tab fits without internal scroll on a 1080p display.
- **Generalised UI wording**: "verzorgers / caregivers / Pflegekräfte / soignants / cuidadores" → "operators / Bediener / opérateurs / operadores" across all five languages, plus broader vertical examples (office, reception, hotel, school, healthcare) in PrivacyDefault help text.

---

## Pre-release engineering pass

A code-review sweep before tagging surfaced four HIGH-severity issues, all fixed before the build:

| ID | Symptom | Root cause | Fix |
|---|---|---|---|
| H1 | After a long-press mass-mask, the first individual unmask cleared `_activePin`; subsequent reveals went through with no PIN — a security regression. | `ApplyMassMask` bumped the PIN-service counter once for the whole batch; the per-tile drain ratio was off. | Bump the counter once per newly-masked tile (`OnMaskApplied()` in a loop), plus prompt for a session PIN if the policy requires one and none is active — mirroring `ApplyMaskLocal`. |
| H2 | The 500 ms long-press `DispatcherTimer` could orphan and re-fire forever if a mouse-up was missed (modal / UAC / focus loss). | A new timer was allocated on every mouse-down without stopping a previous one; the old timer's `Tick` handler kept running. | Single reusable timer field; `Stop()` at the top of every `OnRootMouseDown` and Stop on `Tick` itself. (Also closes L3: per-click allocation.) |
| H3 | Long-running kiosks accumulated dead `Strings.Instance.PropertyChanged` subscriptions over weeks of uptime. | `StreamChoices` constructor subscribed an inline lambda capturing `this`; the subscription was never released. | `StreamChoices` is now `IDisposable`, stores its handler in a field, unsubscribes on `Dispose`; `SetupWindow.Closed` disposes it. |
| H4 | A live `Privacy.Mode` flip via Apply silently desynced the running tiles from the new mode (auto-timers anchored against the wrong direction). | `ApplyChangedSettings` swapped `_privacy` but didn't re-prime tile state for a mode flip; `HasStructuralChange` didn't flag mode changes. | `HasStructuralChange` now treats any `Privacy.Mode` change as structural so the operator gets the existing restart prompt. |

Plus three medium fixes:

- **M1**: dead `_lastCameraNames` field on `NvrSession` removed (superseded by the `TaskCompletionSource` mechanism).
- **M6**: `Pull names` sync no longer aborts on the first failing NVR — it now collects per-NVR errors, keeps fetching reachable recorders, and surfaces a combined message.
- **M9**: redundant `CameraGrid.CommitEdit` in the Save flow removed.

Remaining MEDIUM and LOW findings (dead localized strings, minor allocation in the GDK callback path, etc.) are tracked for a v1.3.x point release.

---

## Critical fix during development: ConfigService persistence

Mid-development testing surfaced that **none of the new v1.3 fields actually persisted to disk**. The fix:

`ConfigService.StoredCamera` and `ConfigService.StoredPrivacy` were the v1.2 shape:

- `Save` projected only the v1.2 columns into JSON → every v1.3 toggle silently disappeared at write time.
- `Load` read only the v1.2 columns → defaults were re-applied at next start.
- The Apply button calls `Load()` right after `Save()`; both paths dropped the new fields, so any v1.3 toggle change "did nothing".

Added to the round-trip:

- `StoredCamera.NvrTitle` (string)
- `StoredPrivacy.ShowMassUnmaskConfirm` (bool)
- `StoredPrivacy.Mode` (int, mirror of the `PrivacyMode` enum)
- `StoredPrivacy.PrivacyDefaultRequireAuthOnReveal` (bool)
- `StoredPrivacy.ShowCameraLabel` (bool, default true)
- `StoredPrivacy.ShowNvrTitle` (bool)

Existing v1.2 configs without these properties keep loading: System.Text.Json fills the missing keys with the property defaults (false / 0 / OversightDefault / true for ShowCameraLabel), matching the v1.2 behaviour exactly so nothing observable changes for configs that pre-date this commit.

---

## Installer changes

- **Self-contained MSI** (109 MB) bundles .NET 9 + WindowsDesktop + ASP.NET Core. Installs on a clean Windows 10/11 x64 without requiring a separate runtime install — important for SCCM / Intune deployments that don't pre-stage the runtime.
- **EV-Authenticode signed**: every `InteractiveMask.*.exe` and `*.dll` plus the MSI itself, with SHA-256 file digest and SHA-256 timestamp digest. SmartScreen recognises the publisher (IDIS Nederland BV) without warnings.
- **Optional `INTERACTIVEMASK_LANGUAGE` MSI property** for unattended language pre-seed (see Headline features §1).

---

## Breaking changes

**None.**

All v1.2 configurations load unchanged. Default values for the new fields match v1.2 behaviour exactly:

| Field | Default in v1.3 | Behaviour matches |
|---|---|---|
| `Privacy.Mode` | `OversightDefault` | v1.2 (always oversight) |
| `Privacy.ShowCameraLabel` | `true` | v1.2 (label always shown) |
| `Privacy.ShowNvrTitle` | `false` | v1.2 (NVR title not shown) |
| `Privacy.ShowMassUnmaskConfirm` | `false` | v1.2 didn't have this; new gesture starts without confirm dialog |
| `Privacy.PrivacyDefaultRequireAuthOnReveal` | `false` | only relevant in PrivacyDefault mode |
| `CameraSlotSettings.NvrTitle` | `""` | v1.2 didn't have this; bottom bar simply doesn't show NVR title until first sync |

---

## Upgrade procedure

1. Install `InteractiveMask-1.3.0.msi`. Existing service is stopped; new files replace v1.2; service restarts. No manual config touch required.
2. (Optional) Open Setup → Cameras → *Pull names from NVR* once, then click Save. Persists the NVR-side camera titles into `config.json` so they show up immediately on every restart.
3. (Optional) Setup → Privacy → *Tile overlay*: enable *Show NVR title* if you want the recorder-side name visible alongside (or instead of) the operator label.
4. (Optional) Setup → Privacy → *Default visibility*: switch to *Privacy default* if your environment prefers blurred-by-default monitoring. Restart on save.

---

## Acknowledgements

Thanks to the demo audience for the directional feedback that shaped this release: the call for broader-than-zorg framing in the UI text, the practical caregiver-leaving-the-station scenario that drove mass-mask, and the camera-name-from-NVR ask that turned into the new sync flow. The aspect-ratio fix came from a real-world demo with a mixed sensor fleet (4:3 + 1:1 fish-eye + 21:9) where UniformToFill cropping was hiding parts of the cameras' field of view.
