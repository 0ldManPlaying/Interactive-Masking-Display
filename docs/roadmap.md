# InteractiveMask Roadmap

Levend document. Items hier landen pas in een release nadat ze door het product team zijn bevestigd en in een sprint zijn ingepland. Meest recente update: 8 mei 2026, na het taggen van v1.3.0.

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

Korte-termijn point releases. Geen functionele wijzigingen, alleen polish en cleanup.

### v1.3.1 kandidaten

| Bron | Onderwerp | Inschatting |
|---|---|---|
| Pre-release M2/M3/M4 | Dead localized strings (NavGrid, GridPageTitle, AdminLangNl/En, StatusError) opruimen in alle 5 talen | 30 min |
| Pre-release M5 | PrimePrivacyDefaultState bumpt nu de PIN-counter niet; consistent maken met OnMaskApplied per tegel | 30 min |
| Pre-release M7 | ApplyCameraChangesLive doet onnodige UpdateLabel + NvrTitle-assign voor onveranderde tegels | 1 uur |
| Pre-release M8 | FetchCameraNamesAsync gooit altijd TimeoutException, ook bij externe cancellation. Differentiëren met TaskCanceledException | 30 min |
| Pre-release M10 | Hard-coded Dutch error string in Help-open path. Loc-key toevoegen | 30 min |
| Pre-release L2 | Per-frame closure-allocation in TileViewModel.OnFrameDecoded (preexisting, niet v1.3-regressie) | 2 uur, alleen bij bewezen GC-pressure |
| Field feedback | Hogere-resolutie IDIS-logo (2048 x 1024 of vector) zodra aangeleverd | 5 min vervangen |

**Doel:** v1.3.1 binnen 2 weken na v1.3.0 als er een live-deployment incident komt; anders bundelen tot één v1.3.1 over een paar weken.

---

## v1.4.0 doorkijk 💡

Nog niet ingepland. Verzameld uit demo-feedback, klantgesprekken en tijdens v1.3.0 opgekomen ideeën.

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

## Architectuur-evoluties (v2.x denken)

Out-of-scope voor v1.x. Vastgelegd zodat we ze niet vergeten.

### Hot-swappable Display zonder restart

Op dit moment vraagt elke structurele wijziging (NVR-fleet, camera-bindings, grid-grootte, privacy-mode) een Display-restart. Voor 24/7 zorgomgevingen is een restart-vrije live-apply path waardevol. Kost een aanzienlijke refactor van NvrSession-lifecycle en het GDK-decoder slot management.

### Cross-platform overweging

Huidige stack is .NET 9 + WPF + IDIS GDK x64. WPF is Windows-only. Een mogelijke v2.0 zou Avalonia + .NET 9 + IDIS GDK kunnen zijn voor Windows / Linux dekking, mits IDIS GDK een Linux-build levert. Geen concrete vraag op dit moment, alleen genoteerd.

### IPC herzien

Named-pipe + length-prefixed JSON werkt prima single-machine. Voor distributie (één Display, web-clients op andere machines) zou een gRPC of WebSocket transport meer zin maken. Niet nu nodig.

---

## Contact

Voor inhoudelijke roadmap-vragen of nieuwe wensen: support@bnl.idisglobal.com. Voor live-deployment problemen: GitHub Issues op de [project-repo](https://github.com/0ldManPlaying/Interactive-Masking-Display) of dezelfde mail.
