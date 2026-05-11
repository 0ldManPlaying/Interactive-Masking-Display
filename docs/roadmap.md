# InteractiveMask Roadmap

Levend document. Items hier landen pas in een release nadat ze door het product team zijn bevestigd en in een sprint zijn ingepland. Meest recente update: 11 mei 2026, na v2.0.0 release. Hele v2.0-stack (AI-driven masking, YOLO26s-seg, centralized inference coordinator, per-camera config dialog) draait stabiel en is getagged als `v2.0.0`.

## Status legend

| Symbool | Betekenis |
|---|---|
| ✅ | Gereleased |
| 🛠 | In ontwikkeling |
| 📋 | Ingepland voor de volgende sprint |
| 💡 | Idee, nog niet ingepland |
| ⏸ | Uitgesteld of geparkeerd |

---

## v1.3.0 — gereleased 8 mei 2026 ✅

Major feature release. Volledig gerealiseerd. Zie [`docs/release-notes-1.3.0.md`](release-notes-1.3.0.md) voor de uitgebreide release-notes.

| # | Feature | Status | Notes |
|---|---|---|---|
| 1 | Mass-mask en mass-unmask via long-press (500 ms) | ✅ | State-guard tegen accidentele privacy-doorbraak. Volgt bestaande auth-policy. |
| 2 | Privacy-default mode als Setup-toggle | ✅ | OversightDefault (default) en PrivacyDefault keuzes. Mode-flip is structural (vraagt restart). |
| 3 | Drag/drop herordenen in Setup Slot bindings | ✅ | Grip-icoon links per rij. Slot-nummers herrekenen automatisch. |
| 4 | Vijf talen (NL, EN, DE, FR, ES) | ✅ | 226 keys per taal, formele zakelijke toon, live language-switching. |
| 5 | First-run language picker + MSI taal-seed | ✅ | Modaal bij eerste opstart; `INTERACTIVEMASK_LANGUAGE` MSI property voor unattended deployment. |
| 6 | IDIS-watermerk in lege tegels | ✅ | 10% opacity, 400 x 200 PNG (hogere resolutie staat op v1.3.x lijst). |

### Bonus features die mid-sprint toegevoegd zijn

| Feature | Aanleiding |
|---|---|
| **Multi-NVR aware mass-mask** | Werkt cross-NVR; één gesture dekt alle aangesloten recorders. |
| **NVR camera-naam sync** (Pull names) | Demo-feedback. Aparte NvrTitle-kolom + persisted `NvrTitle` veld op CameraSlotSettings; sync via live NVR-sessie zodat geen tweede login nodig is. |
| **Per-tile overlay bar** | NVR-titel links, custom label rechts; beide independent togglebaar via Setup → Privacy → Tegel-overlay. |
| **5×5 grid (25 tegels)** | Naast 1×1 / 2×2 / 3×3 / 4×4. |
| **Apply-knop** in Setup | Wijzigingen toepassen zonder Setup te sluiten; live iteratie tijdens config. |
| **About-tab** | Versie, publisher, copyright, integraties, open-source licenties, mailto bug-rapport. |
| **Aspect-ratio fix** | Stretch=UniformToFill → Uniform, zodat 4:3 / 5:4 / 1:1 / 21:9 camera's volledig in beeld blijven. |
| **Generieke UI-wording** | "verzorgers / bewoners" → "operators / tegels" door alle vijf talen. |

### Pre-release engineering pass

| ID | Severity | Onderwerp | Status |
|---|---|---|---|
| H1 | High | Mass-mask PIN-bookkeeping security regression | ✅ gefixt |
| H2 | High | Long-press timer leak bij gemiste mouse-up | ✅ gefixt |
| H3 | High | StreamChoices Strings.PropertyChanged leak | ✅ gefixt |
| H4 | High | Privacy.Mode flip silent desync | ✅ gefixt (nu structural) |
| M1 | Medium | Dead `_lastCameraNames` veld in NvrSession | ✅ verwijderd |
| M6 | Medium | Pull-names abortte op eerste falende NVR | ✅ continue + accumuleer errors |
| M9 | Medium | Dubbele `CameraGrid.CommitEdit` in save | ✅ verwijderd |

### Kritieke fix tijdens ontwikkeling

ConfigService.StoredCamera + StoredPrivacy waren in v1.2-vorm blijven hangen, waardoor alle nieuwe v1.3 velden silent dropte tijdens de save/load round-trip. Gefixt door uitbreiding van beide DTO's en hun ApplyStored/ToStored mappingen.

---

## v1.3.x patches 📋

Bundeling met v2.0.x. Doorgelopen pre-release items uit de v1.3 review en de losse code-analyse bevindingen staan nu in de **v2.0.x patches** sectie hieronder (carry-over polish-tabel). Geen aparte v1.3.x release-track meer.

---

## Toekomstige ideeën — niet aan een release gebonden 💡

Verzameld uit demo-feedback, klantgesprekken en gedurende v1.3 / v2.0 opgekomen ideeën. Komt op de roadmap zodra een release-window vastgesteld wordt.

### Hoog op de wensenlijst

| Idee | Bron | Score |
|---|---|---|
| **Help-content vertalen naar DE / FR / ES** (8 hoofdstukken) | v1.3 vertaalwerk; momenteel fallback naar EN voor die drie talen | Vraagt externe vertaler; ~1 week doorlooptijd |
| **Long-press duur configureerbaar** in Setup | Sommige users willen 300 ms (sneller) of 750 ms (minder accidental) | Half dag werk |
| **Drag/drop in live-view voor eindgebruikers** | Admin-toggle in Setup om dit aan te zetten; volgorde wijziging vraagt auth | 1 dag werk |
| **Audit-log review GUI** | Huidige Audit-tab toont laatste N events; filter op type / NVR / tijdvenster + zoekveld zou helpen | 2 dagen werk |
| **Snapshot-export naar PDF** | Eerder geïdentificeerd in 2026-05; rapport van actuele tile-states + privacy-status voor dossier | 2-3 dagen |
| **Vector/SVG logo support** voor scherper renderen op 4K | 1x1 fullscreen-grid op 4K is met 400 x 200 PNG niet ideaal | 1 dag |

### Strategische ideeën

| Idee | Bron | Open vragen |
|---|---|---|
| **Microsoft Store / Intune sideload distributie** | Eerder besproken in v1.0 traject; geparkeerd voor multi-NVR | App-package (.msix) builden; signing apart van EV-cert traject |
| **PTZ-bediening voor PTZ-camera's** | IDIS GDK ondersteunt het, geen integratie in InteractiveMask | UX vraag: hoe past een PTZ-control in een privacy-mask kiosk? |
| **Mobiele begeleidende app** | Andere lange-termijn idee | iOS / Android, of progressive web app via WebHost? |
| **Multi-monitor support** | Voor grotere zorgposten met meerdere schermen | Huidige Display is single-monitor; vraagt UI-rework |
| **Audit-log syslog filtering** | Per event-type besluiten of het naar SIEM gestuurd wordt | Setup uitbreiding + bounded-channel logica |
| **Custom blur-styles** | Sommige klanten willen pixelate, mozaiek, of zwart-vlak in plaats van Gaussian blur | XAML BlurEffect → custom shader of overlay-rect |

### Geparkeerd ⏸

| Item | Reden |
|---|---|
| Custom WiX UI dialog voor taal-keuze tijdens install | Vervangen door registry-seed property + first-run picker (v1.3.0). Kan terug op de roadmap als enterprise-customers hier toch om vragen. |
| Drag/drop voor eindgebruikers in live-view | Bewust uitgesteld in v1.3 scope-besluit. Komt terug bij v1.4 als admin-toggle. |

---

## v2.0.0 — gereleased 11 mei 2026 ✅

AI-driven object-level masking als headline-feature. Vraag van een Nederlandse systeem-integratie partner namens een grootzakelijke retail-eindklant, 11 mei 2026. De eindklant heeft outdoor-cameras op distributiecentra en winkellocaties waar voertuigen passeren. GDPR-conform willen zij kentekens standaard maskeren in live-view, met reveal-on-demand voor geautoriseerde reviewers. De use case generaliseert naar een bredere productrichting: object-level privacy in plaats van tegel-level.

Volledige release-notes: [`docs/release-notes-2.0.0.md`](release-notes-2.0.0.md).

Volledig architectuurdocument: [`docs/architecture-v2-ai.md`](architecture-v2-ai.md).

### Scope-principes

- **Bestaande v1.x-functionaliteit blijft 1-op-1 intact.** AI-features zijn opt-in en alleen beschikbaar als de hardware het trekt. Klanten zonder GPU/NPU draaien InteractiveMask 2.0 in compatibility-mode (equivalent aan v1.x).
- **Privacy-first fail-safe.** Bij detector-uitval, overload of timeout valt elke betreffende tegel terug op volledige-tegel blur (huidige v1.x-gedrag). Geen privacy-leak als de AI-laag faalt.
- **Geen ANPR.** Geen OCR, geen plate-string-opslag, geen plate-watchlist. Alleen detection en segmentation. Plate-strings worden nooit gelezen of geëxporteerd.
- **Capability-gated.** AI-opties zijn alleen in Setup toegankelijk als de host de minimaal vereiste tier haalt. Runtime-monitoring degradeert automatisch bij overload.
- **Realistisch maximum: 16 streams (4x4-grid)** voor AI-features. De 5x5-grid (25 tegels) blijft beschikbaar in v1.x-modus voor één bekende klant zonder AI-vraag.

### Object-categorieën

Drie masking-categorieën in v2.0, afgeleid van het YOLO26s-seg detection + segmentation model:

| Categorie | UI-kleur | COCO-bron-labels |
|---|---|---|
| Person | rood `#E74C3C` | `person` (0) |
| TwoWheeler | oranje `#F39C12` | `bicycle` (1), `motorcycle` (3) |
| Vehicle | blauw `#3498DB` | `car` (2), `bus` (5), `truck` (7) |

Face-detection is in de v2.0-cyclus geprobeerd (YuNet) en weer weggehaald: kleine gezichten op security-camera-afstand bleken te onbetrouwbaar voor productie. LicensePlate verschuift naar v2.0.x (vereist in-house Roboflow-getraind model). Bron-labels (COCO class ID's) worden bewaard op elke `Detection`-record voor audit en diagnose (zodat support kan zien dat een Vehicle bijvoorbeeld een `truck` was).

### Hardware-baselines

| Baseline | Doel | Implementatie |
|---|---|---|
| Windows + dGPU | Intel Core i5 (12e gen of later) + NVIDIA RTX 4060 8 GB of beter | In-process ONNX Runtime met DirectML of CUDA EP. Zelfde installer en executable als v1.x. |
| Jetson Orin Nano 8GB | Edge-appliance naast bestaande Windows-host | Sidecar-service op Linux ARM64, TensorRT-geoptimaliseerd, levert detectie-metadata over netwerk-IPC aan de Display-PC. |

### Gefaseerde uitrol

Sequencing-besluit (11 mei 2026): Windows-baseline eerst volledig productie-rijp, daarna pas ARM-port. Voorkomt dat twee build-pijplijnen en twee runtime-stacks tegelijk worden gestabiliseerd.

| Fase | Inhoud | Status |
|---|---|---|
| v2.0 | Multi-class detection + segmentation op Windows + dGPU (Baseline A): Person + TwoWheeler + Vehicle via YOLO26s-seg (NMS-free, prototype masks). Per-camera config (toggle, klassen, confidence-slider met hysteresis, mask-padding, mask-stijl color-coded vs source-blur, opacity slider, ROI polygon) | ✅ gereleased 11 mei 2026 |
| v2.0.x | LicensePlate-categorie + Reveal-flow + Adaptive-degradation (zie "Nog open binnen v2.0-scope" hieronder) | 📋 ingepland |
| v2.1 | Jetson Orin Nano ARM64 port (Baseline B), zelfde feature-set als v2.0(.x) | 💡 start pas zodra v2.0 op Windows in productie stabiel is |
| v2.x | Semantic segmentation upgrade, SAM2-class voor pixel-perfecte contouren | 💡 deels achterhaald — YOLO26s-seg geeft al prototype-mask segmentation; SAM2 alleen voor precisie-verfijning |

### Prerequisite-werk (kan al starten voor v2.0-scope vaststaat)

| ID | Component | Status |
|---|---|---|
| P1 | Roadmap- en architectuurdocumentatie | ✅ vastgelegd (deze documenten) |
| P2 | `HostCapabilityProfile` resource-probe (WMI, registry-VRAM, NPU-detectie) | ✅ commit `3bb0cd9` |
| P3 | Benchmark-runner (ONNX Runtime + DirectML, MobileNetV2 reference workload) | ✅ commit `ebf994d` |
| P4 | `IObjectDetector`-abstractie + `NullDetector` fail-safe-bridge naar full-tile blur | ✅ commit `7c13950` |
| P5 | IPC-protocol-specificatie voor Jetson-sidecar (gRPC + mTLS) | 💡 ontwerp blijft staan voor v2.1 |

### Main work (v2.0 op Windows-baseline) — gerealiseerd

| ID | Component | Status |
|---|---|---|
| M1 | YuNet face-detection als eerste werkend detector-backend | ✅ commit `bb81f68` (later vervangen omdat kleine gezichten op security-camera-afstand onbetrouwbaar bleken) |
| M2 + M3.1 | YOLOv8n COCO multi-class detection + echte regio-blur (`ImageBrush` + `BlurEffect`) | ✅ commit `bcd0f9a` |
| M3.2 v2 | **Centralized inference coordinator**: één worker bezit de `InferenceSession`, per-stream slot-replacement, geen concurrent ORT-calls meer (was bron van eerdere native crashes) | ✅ commit `c4824d7` |
| M3.3 | Per-camera mask-padding (0-50%) + YOLO11n forward-compat path | ✅ commit `f2c1781` |
| Fase A+B | Per-camera AI on/off + class-filter (Person/TwoWheeler/Vehicle) + per-camera modal dialog | ✅ commit `9192aeb` |
| M3.4 | ROI polygon per camera met drag-to-move editor + bbox-centroid filter | ✅ commits `bdd39bb` + `439c899` |
| ORT bump | Microsoft.ML.OnnxRuntime.DirectML 1.20.1 → 1.24.4 (opset 22 support, vereist voor YOLO26 modellen) | ✅ commit `2a74ed3` |
| M3.5 | Segmentation masks (YOLO26n-seg) + colour-coded silhouettes per klasse (Person rood, TwoWheeler oranje, Vehicle blauw) + radius-40 privacy blur | ✅ commits `96c581e` + `d9c9987` |
| YOLO26 baseline | **YOLO26s-seg** als productie-model (NMS-free, 300 detection slots, 32-channel proto-masks @ 160×160). Per-camera confidence-slider met hysteresis (15-70%, default 40%, IoU≥0.4 frame-matching) tegen flicker op kleine objecten | ✅ commit `f9f3d9b` |
| v2.0 polish | Per-camera mask-stijl toggle (color-coded vs source-blur CCTV look) + per-camera mask-opacity slider (20-100%) + CameraAiSettingsDialog herontwerp in app-stijl (Cards, ToggleSwitch, CategoryChip pills, SegmentedOption) | ✅ commit `f0a16b6` + fixes `aee4545` / `b5b2e16` |
| YOLO11n model | Ultralytics export laadt op ORT 1.24 maar valt terug op CPU voor onbekende ops → ~6x slowdown | ⏸ geparkeerd, niet meer relevant: YOLO26s-seg vervangt YOLO11n als architectuur-keuze |

### Audit-events in v2.0

Toegevoegd in audit-log (`%PROGRAMDATA%\InteractiveMask\audit.log`):

- `AiDetectorInit` — backend + actief model bij succesvolle init
- `AiDetectorFault` — init-failure of niet-herstelbare runtime-fault (Display draait door op v1.x)
- `AiDetectorStopped` — graceful dispose, met evt. dispose-error detail

Nog te implementeren in v2.0.x (zie kandidatenlijst hieronder):

- `ai.detector.degraded` / `ai.detector.restored` — gekoppeld aan adaptive-degradation
- `ai.reveal.requested` / `ai.reveal.expired` — gekoppeld aan reveal-flow

---

## v2.0.x patches 📋

Korte-termijn point releases na de v2.0.0-tag. Twee sporen: functionele AI-uitbreidingen en carry-over polish.

### Functionele AI-uitbreidingen (v2.0.x feature-batch)

| ID | Item | Inschatting | Bron |
|---|---|---|---|
| F1 | **LicensePlate-categorie** — `ObjectClass.LicensePlate` enum staat al klaar; vereist alleen het in-house Roboflow-getraind plate-detector model en `CocoMap`-uitbreiding bij integratie | wachten op model + ~1 dag | retail-partner request (oorspronkelijke v2.0-aanleiding) |
| F2 | **Reveal-flow voor AI-mask** — per-tegel tijdelijke AI-uitschakeling met PIN/AD-auth, duur-keuze (30 s / 1 min / 5 min / tot expliciete re-mask), audit-events `ai.reveal.requested` + `ai.reveal.expired` | 2 dagen | architectuur §12 |
| F3 | **Adaptieve degradatie-ladder** — runtime-monitor leest latency / queue-depth / GPU-load, scaalt rate (15→10→5→1 fps) / resolutie (640→480→320) / mask-type (polygon→bbox) trapsgewijs af bij overload. Audit-events `ai.detector.degraded` + `ai.detector.restored`. Niet kritisch in praktijk (coordinator + per-camera-config dempen al veel) maar vereist voordat we 16-stream deployments officieel ondersteunen | 3 dagen | architectuur §8 |

### Carry-over polish (v1.3 pre-release review)

Niet gekoppeld aan AI-werk. Kandidaten voor combinatie in een v2.0.1.

| Bron | Onderwerp | Inschatting | Status |
|---|---|---|---|
| ~~Pre-release M2/M3/M4~~ | ~~Dead localized strings (NavGrid, GridPageTitle, AdminLangNl/En, StatusError) opruimen~~ | ~~30 min~~ | ✅ commit `20954d8` |
| Pre-release M5 | `PrimePrivacyDefaultState` bumpt PIN-counter niet; consistent maken met `OnMaskApplied` per tegel | 30 min | 📋 |
| Pre-release M7 | `ApplyCameraChangesLive` doet onnodige `UpdateLabel` + `NvrTitle`-assign voor onveranderde tegels | 1 uur | 📋 |
| ~~Pre-release M8~~ | ~~`FetchCameraNamesAsync` gooit altijd `TimeoutException`, ook bij externe cancellation; differentiëren met `TaskCanceledException`~~ | ~~30 min~~ | ✅ commit `20954d8` |
| ~~Pre-release M10~~ | ~~Hard-coded Dutch error string in Help-open path; loc-key toevoegen~~ | ~~30 min~~ | ✅ commit `20954d8` |
| Pre-release L2 | Per-frame closure-allocation in `TileViewModel.OnFrameDecoded` (preexisting, niet v1.3-regressie) | 2 uur | ⏸ alleen bij bewezen GC-pressure |
| Code-analyse #9 | `AuditLog.Write` doet sync file-IO op UI-thread (lange-levende `StreamWriter` ipv `File.AppendAllText`) | 1 uur | ⏸ "laat staan" tenzij merkbare hapering |
| Code-analyse #10 | `WebAccessSessionStore` heeft geen periodieke sweep van verlopen tokens | 30 min | 📋 |
| Field feedback | Hogere-resolutie IDIS-logo (2048 × 1024 of vector) zodra aangeleverd | 5 min vervangen | 💡 |

---

## Architectuur-evoluties (v2.x en later)

Out-of-scope voor v2.0 en niet gebonden aan een specifiek releasespoor. Vastgelegd zodat we ze niet vergeten.

### Hot-swappable Display zonder restart

Op dit moment vraagt elke structurele wijziging (NVR-fleet, camera-bindings, grid-grootte, privacy-mode) een Display-restart. Voor 24/7 deploymentomgevingen is een restart-vrije live-apply path waardevol. Kost een aanzienlijke refactor van `NvrSession`-lifecycle en het GDK-decoder slot management.

### Cross-platform overweging

Huidige stack is .NET 9 + WPF + IDIS GDK x64. WPF is Windows-only. Eerder genoteerd als mogelijke Avalonia-port; sinds v2.0 deels achterhaald omdat de Jetson-sidecar-aanpak in v2.1 een ARM64/Linux-pad biedt zonder de Display-UI te porten. Volledige cross-platform Display blijft een open vraag voor een hypothetische v3.0.

### IPC herzien

Named-pipe + length-prefixed JSON werkt prima single-machine voor Display ↔ WebHost. v2.0 ship in-process detection (geen netwerk-IPC nodig); de v2.1 Jetson-sidecar introduceert gRPC over TLS naast deze pipe. Op termijn zou een unificatie naar gRPC voor beide kanalen kunnen, mits er een concrete distributie-vraag komt.

---

## Contact

Voor inhoudelijke roadmap-vragen of nieuwe wensen: support@bnl.idisglobal.com. Voor live-deployment problemen: GitHub Issues op de [project-repo](https://github.com/0ldManPlaying/Interactive-Masking-Display) of dezelfde mail.
