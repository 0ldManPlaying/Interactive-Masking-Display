# InteractiveMask v2.0: Architectuur

**Versie:** 0.1 (concept)
**Datum:** 11 mei 2026
**Status:** Werkdocument. Wordt gefixeerd zodra v2.0-implementatie start.
**Eigenaar:** IDIS Nederland BV

---

## 1. Doel en context

InteractiveMask v2.0 introduceert AI-gestuurde object-level privacy-masking als optionele uitbreiding op de bestaande tegel-level masking uit v1.x. Concrete aanleiding: een Nederlandse systeem-integratie partner heeft namens een grootzakelijke retail-eindklant gevraagd om automatische blurring van kentekens in live-view, GDPR-conform. Het architectuurontwerp is bewust generiek opgezet zodat dezelfde pipeline op termijn gezichten, personen en andere objectklassen kan maskeren.

Dit document beschrijft architectuurkeuzes, niet implementatiedetails. Codedetails verschijnen in vervolg-iteraties zodra deelcomponenten worden gerealiseerd.

---

## 2. Ontwerpprincipes

1. **Backward-compatible.** Een installatie van v2.0 op een host zonder geschikte GPU/NPU gedraagt zich functioneel identiek aan v1.3. AI-features zijn niet zichtbaar als ze niet ondersteund worden.
2. **Privacy fail-safe.** Elke faalmodus (detector-crash, timeout, overload, model-load-failure, netwerkverlies bij remote detector) leidt tot teruggang naar full-tile blur op de betreffende tegels. Een gemiste detectie mag nooit resulteren in een ongemaskerd object.
3. **No-ANPR.** De detector classificeert en lokaliseert objecten; hij leest of exporteert nooit plate-strings. Geen OCR-stap, geen plate-watchlist, geen koppeling tussen plate-content en gebruikersacties.
4. **Capability-gated en adaptief.** Hardware-detectie bepaalt welke AI-opties beschikbaar zijn. Runtime-monitoring degradeert automatisch bij overload.
5. **Pluggable detector-backends.** Dezelfde Display ondersteunt zowel in-process detectie (Windows + GPU) als remote detectie (Jetson-sidecar). Toekomstige backends (Hailo M.2, Coral USB, Intel NPU via OpenVINO) volgen hetzelfde contract.

---

## 3. Hardware-baselines

### 3.1 Baseline A: Windows + dGPU (in-process)

| Component | Minimum | Aanbevolen |
|---|---|---|
| CPU | Intel Core i5, 12e gen of later (P+E cores) | i7 13e gen of later |
| RAM | 16 GB DDR4 | 32 GB DDR5 |
| GPU | NVIDIA RTX 4060 8 GB | RTX 4070 12 GB of hoger |
| Storage | 256 GB NVMe, 10 GB vrij voor modellen | 512 GB NVMe |
| OS | Windows 10 22H2 of Windows 11 23H2 | Windows 11 24H2 |
| Runtime | .NET 9, ONNX Runtime 1.18+, DirectML 1.15+ of CUDA 12.x | idem |

### 3.2 Baseline B: Jetson Orin Nano 8GB (sidecar) — gepland voor v2.1

Sequencing-besluit (11 mei 2026): Baseline B wordt pas geïmplementeerd nadat Baseline A volledig productie-rijp is. De architectuur (detector-abstractie, IPC-protocol) wordt wel meteen meegenomen zodat v2.0-code geen herontwerp nodig heeft voor de v2.1-port.


| Component | Minimum | Aanbevolen |
|---|---|---|
| Module | Jetson Orin Nano 8 GB Developer Kit | Jetson Orin NX 16 GB |
| Storage | 64 GB NVMe | 256 GB NVMe |
| OS | JetPack 6.x (Ubuntu 22.04 ARM64) | idem |
| Runtime | TensorRT 10.x, Python 3.10 of Rust-binding | idem |
| Netwerk | 1 Gbps Ethernet naar Display-PC, latency < 5 ms | idem, gescheiden VLAN |

De Display-PC bij Baseline B mag een eenvoudige Windows-host zonder dGPU zijn, want al het zware werk gebeurt op de Jetson.

---

## 4. Logisch componentdiagram

```
┌─────────────────────────────────────────────────────────────┐
│ InteractiveMask.Display (WPF, .NET 9, Windows)              │
│                                                             │
│ ┌─────────────┐    ┌──────────────┐    ┌──────────────────┐ │
│ │ IDIS GDK    │───▶│ Frame router │───▶│ Render pipeline  │ │
│ │ decoder     │    │  + timestamp │    │ (WriteableBitmap │ │
│ │ (per NVR)   │    │              │    │  + blur overlay) │ │
│ └─────────────┘    └──────┬───────┘    └──────────────────┘ │
│                           │                     ▲           │
│                           ▼                     │           │
│                    ┌──────────────┐    ┌────────┴────────┐  │
│                    │ IObjectDetector ─▶│ Detection cache │  │
│                    │ abstraction  │    │ (per stream,    │  │
│                    └──────┬───────┘    │  timestamped)   │  │
│                           │            └─────────────────┘  │
│      ┌────────────────────┼───────────────────────┐         │
│      ▼                    ▼                       ▼         │
│ ┌─────────┐         ┌──────────┐            ┌──────────┐    │
│ │ Local   │         │ Remote   │            │ Null     │    │
│ │Detector │         │Detector  │            │Detector  │    │
│ │(ONNX RT)│         │(Jetson)  │            │(fallback)│    │
│ └─────────┘         └────┬─────┘            └──────────┘    │
└──────────────────────────┼──────────────────────────────────┘
                           │ gRPC over TLS (mTLS)
                           ▼
                  ┌──────────────────┐
                  │ Jetson sidecar   │
                  │ - decode (NVDEC) │
                  │ - TensorRT infer │
                  │ - polygon out    │
                  └──────────────────┘
```

---

## 5. Detector-abstractie

Geïmplementeerd in `InteractiveMask.Detection`. De definitieve types matchen het concept hieronder en zijn live in `DetectionContracts.cs` + `IObjectDetector.cs`. Type-rename: het record dat detecties bevat heet `DetectedObject` (niet `Detection`) om een type/namespace-collisie te vermijden bij consumers die ook de namespace `InteractiveMask.Detection` importeren.

**Belangrijke afwijking van het oorspronkelijke ontwerp**: `OnnxLocalDetector` is geïmplementeerd als dunne facade over een nieuwe `InferenceCoordinator` (zelfde assembly). De coordinator bezit één gedeelde `InferenceSession` en draait alle `Run()`-calls op één worker-task; per-stream slot-replacement zorgt dat nieuwere frames oude pending submissions vervangen. Achtergrond: het eerste ontwerp ging uit van per-tile concurrent submissions naar dezelfde `InferenceSession` — dat veroorzaakte native AccessViolation-class crashes onder DirectML EP. De gecentraliseerde aanpak elimineert deze contentie volledig en geeft tegelijk voorspelbare per-tegel cadens. Zie commit `c4824d7` voor de refactor.

```csharp
public interface IObjectDetector : IDisposable
{
    DetectorCapability Capability { get; }
    DetectorStatus Status { get; }

    Task InitializeAsync(DetectorConfig config, CancellationToken ct);
    ValueTask<DetectionFrame> DetectAsync(FrameRef frame, CancellationToken ct);
}

public record DetectionFrame(
    long FrameTimestampTicks,
    IReadOnlyList<Detection> Detections,
    DetectorMetrics Metrics);

public record DetectedObject(
    ObjectClass Class,        // Masking-categorie: Face / Person / TwoWheeler / Vehicle / LicensePlate
    string? RawClassLabel,    // Model-native label voor audit (bv. "car", "truck", "bicycle")
    float Confidence,
    BoundingBox Box,
    Polygon? Mask);           // null in bbox-only modus

public enum ObjectClass
{
    Unknown      = 0,
    Face         = 1,
    Person       = 2,
    TwoWheeler   = 3,   // bicycle + motorcycle
    Vehicle      = 4,   // car + bus + truck
    LicensePlate = 5,
}
```

De vijf categorieën zijn afgeleid van pretrained- of in-house-modellen (volledige tabel in `docs/roadmap.md` onder "Object-categorieën"). Vier categorieën komen uit pretrained models (YuNet voor faces, YOLOv8n COCO voor de overige drie) en zijn beschikbaar in v2.0. LicensePlate vraagt in-house Roboflow-training en landt in v2.0.x zodra het model gereed is.

Implementaties:

- **`OnnxLocalDetector`** (v2.0): ONNX Runtime in-process, DirectML of CUDA EP, model uit `models/` directory. Model geladen bij eerste use, hot-reload bij Setup-apply.
- **`NullDetector`** (v2.0): faal-stand-in. Retourneert lege detectie-lijst met status `Unavailable`. Render pipeline interpreteert dit als trigger voor full-tile blur op gebonden tegels.
- **`JetsonRemoteDetector`** (v2.1): gRPC-client naar Jetson-sidecar, mTLS-versleuteld, heartbeat elke 1 s. Interface en stub-implementatie meegenomen in v2.0 zodat de v2.1-port geen herontwerp vraagt.

De Display kent enkel `IObjectDetector` en weet niet welk type backend actief is, behalve voor de capability-display in Setup en de status-indicator op de tegel.

---

## 6. Frame-sync en render-pad

Kernuitdaging: de detector produceert detecties met latency (intern of via netwerk). De Display rendert continu, ook op frames waarvoor de detector nog niet klaar is.

### Strategie

1. Elk gedecodeerd frame krijgt een monotonic timestamp bij ontvangst van de IDIS GDK callback.
2. Frame wordt doorgegeven aan zowel de render pipeline (direct) als de detector (asynchroon, queue-depth 1, oudste frame wordt gedropt bij congestie).
3. Detector levert resultaat met dezelfde timestamp terug naar een per-stream `DetectionCache`.
4. Render pipeline kijkt voor elke frame in de cache. Drie scenario's:
   - **Match**: detectie met identieke timestamp aanwezig. Render frame met blur-overlay op de polygonen of bboxes.
   - **Lag**: alleen oudere detectie aanwezig (binnen tolerantie, bijvoorbeeld < 200 ms). Render met de oudere detectie, eventueel met motion-extrapolatie van de bbox. Status-indicator op tegel toont "tracking".
   - **Stale of leeg**: geen recente detectie (> tolerantie of detector niet klaar). Render met full-tile blur. Status-indicator toont "warming up" of "fallback".

Tolerantie is configureerbaar per camera-snelheidsprofiel. Statische cameras (parkeerplaats) mogen ruime tolerantie; cameras met snelbewegend verkeer (oprit, laadkuil) krijgen strakke tolerantie en lopen sneller in fallback.

### Render-detail

Per tegel een `Canvas`-overlay bovenop het frame. Detecties worden als `Path` met `BlurEffect` (of een goedkopere mosaic-fill) geprojecteerd in tegel-coordinaten. De projectie corrigeert voor de actuele `Stretch="Uniform"`-letterbox/pillarbox-marges die in v1.3 zijn geïntroduceerd voor aspect-ratio-correctie.

---

## 7. Resource-probe en capability-tiers

### 7.1 Resource-probe-output

```csharp
public record HostCapabilityProfile(
    DateTime ProbedAt,
    CpuInfo Cpu,
    MemoryInfo Memory,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<NpuInfo> Npus,
    IReadOnlyList<RemoteDetectorEndpoint> RemoteDetectors,
    BenchmarkResult? LastBenchmark,
    CapabilityTier Tier);
```

Bron-API's:

| Veld | Bron |
|---|---|
| CPU | `System.Environment` + `Registry::HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` |
| Geheugen | `GlobalMemoryStatusEx` via P/Invoke |
| GPU | DXGI factory enumeration, `IDXGIAdapter3::QueryVideoMemoryInfo` |
| NPU | WinML `LearningModelDevice::CreateFromExecutionProvider`, vendor-detection (Intel AI Boost, AMD XDNA, Qualcomm Hexagon) |
| Remote | Setup-configuratie + heartbeat-probe |

Profiel wordt eenmalig bepaald bij eerste start, opnieuw bij hardware-wisseling (volume-id-check), en op verzoek vanuit Setup → AI-tab → "Probe opnieuw".

### 7.2 Capability-tier-tabel

Indicatief. Definitieve drempels komen uit benchmark-output (sectie 7.3).

| Tier | Indicator | Enabled AI-features |
|---|---|---|
| 0 | Geen geschikte GPU/NPU, geen RemoteDetector geconfigureerd | Geen. v1.x compatibility-mode. |
| 1 | iGPU (Intel UHD 770, Iris Xe, Radeon 780M), 16 GB RAM, of lichte dGPU < 4 GB VRAM | 1-2 streams, plate-only, 320x320, motion-gated 5 fps, bbox-mask. |
| 2 | dGPU 4-8 GB VRAM (RTX 3050, Arc A380), of iGPU + NPU | 4-9 streams, plate + face, 480x480, 10 fps, bbox-mask. |
| 3 | dGPU 8+ GB VRAM (RTX 4060, Arc A770), of Jetson Orin Nano 8GB remote | Volledige 16 streams (4x4-grid), 640x640, 15 fps, polygon-mask. |
| 4 | dGPU 12+ GB (RTX 4070, Orin NX 16GB) | Tier 3 + alle klassen tegelijk + headroom voor v2.x segmentation-upgrade. |

### 7.3 Benchmark-runner

Eenmalig (of op verzoek) een mini-benchmark:

- Laadt een tiny ONNX-model (bijvoorbeeld MobileNet-classifier, ~5 MB).
- Draait 200 inference-frames op een dummy-bitmap.
- Meet cold-start latency, steady-state p50/p95/p99, throughput per inference-resolutie (320/480/640).
- Persisteert resultaat in `%PROGRAMDATA%\InteractiveMask\benchmark.json`.
- Toont in Setup → About → System capabilities.

Resultaat is doorslaggevend boven de tier-tabel: een ongebruikelijk goede iGPU kan tier 2 halen, een belaste dGPU kan op tier 1 vastlopen.

---

## 8. Adaptieve degradatie

Runtime-monitor draait elke 250 ms en leest:

- Inference-latency p95 per detector
- Queue-depth per stream
- GPU/NPU-utilization (vendor-API of nvml/dxgi)
- Frame-drop-rate render pipeline
- Heartbeat-status remote detector

### Degradatie-ladder

Toegepast in volgorde tot meting weer in groen valt:

1. **Inference-rate verlagen**: 15 → 10 → 5 → 1 fps. Tussen-frames hergebruiken laatste detectie met motion-extrapolatie.
2. **Motion-gating activeren**: per-tegel pixel-diff-drempel; statische tegels skippen detectie volledig.
3. **Inference-resolutie verlagen**: 640 → 480 → 320.
4. **Mask-type degraderen**: polygon → bbox.
5. **Per-camera AI uitschakelen** volgens Setup-prioriteit; betreffende tegel valt terug op full-tile blur.
6. **Globale fallback**: alle AI uit, audit-event geschreven, status-indicator zichtbaar op alle tegels.

Elke degradatie-stap genereert een audit-event en een UI-status-update op de betreffende tegel. Herstel volgt dezelfde ladder in omgekeerde richting, met hysterese om flapping te voorkomen.

---

## 9. Audit-uitbreidingen

Bestaande v1.x audit-event-schema blijft 1-op-1 intact. Toevoegingen voor v2.0:

| Event-type | Payload-velden | Wanneer |
|---|---|---|
| `ai.detector.init` | detector-type, model-id, version, capability-tier | Bij start of wisseling |
| `ai.detector.degraded` | from-step, to-step, reason, metrics-snapshot | Bij degradatie-stap |
| `ai.detector.restored` | restored-to-step, duration-degraded-sec | Bij herstel naar hogere stap |
| `ai.detector.fault` | detector-type, error-class, message | Bij crash, timeout of heartbeat-loss |
| `ai.reveal.requested` | camera-id, source (user/PIN/AD), duration-sec | Bij reveal door reviewer |
| `ai.reveal.expired` | camera-id, reason (timeout/manual) | Bij einde reveal-window |

Belangrijk: **geen detectie-coordinaten, geen plate-content, geen frame-data** in audit. Alleen metadata over de detector-staat en reviewer-acties. Dit houdt het audit-bestand vrij van persoonsgegevens en consistent met het no-ANPR-principe.

---

## 10. IPC-protocol Jetson-sidecar (v2.1)

Deze sectie beschrijft het ontwerp voor de v2.1 ARM-port. In v2.0 wordt alleen de interface-laag (`IObjectDetector` met stub-`JetsonRemoteDetector`) meegenomen, niet de feitelijke gRPC-implementatie.

Transport: gRPC over TLS, mutual auth via client-cert. Service-schets:

```protobuf
service DetectorService {
  rpc StreamDetections(stream SyncMessage) returns (stream DetectionMessage);
  rpc GetCapability(Empty) returns (CapabilityMessage);
  rpc Heartbeat(stream Ping) returns (stream Pong);
}

message SyncMessage {
  int64 timestamp_ticks = 1;
  int32 stream_id = 2;
  // Frame komt NIET over de wire; Jetson decodeert zelf
  // Alleen sync-metadata zodat Display en Jetson dezelfde frame-tijdlijn delen
}

message DetectionMessage {
  int64 timestamp_ticks = 1;
  int32 stream_id = 2;
  repeated Detection detections = 3;
  DetectorMetrics metrics = 4;
}
```

### Architectuurbeslissing: Jetson decodeert zelf

Jetson decodeert de IDIS-stream zelfstandig via de IDIS GDK Linux ARM64-binding (bevestigd 11 mei 2026 als beschikbaar bij IDIS Nederland), Display krijgt alleen detection-metadata. Frames over de wire zou bandbreedte verspillen en de Jetson NVDEC-pipeline omzeilen.

Voordeel van native GDK op de Jetson: dezelfde feature-set als de Windows-Display (fish-eye dewarp, HEVC, alle stream-profielen), zodat detectie-coordinaten en render-coordinaten exact corresponderen zonder transformatie-overhead.

### Certificaten

mTLS-certificaten worden uitgegeven door een lokale CA tijdens Jetson-pairing (via Setup-wizard). Privaatsleutels DPAPI-encrypted aan Display-zijde, file-system-encrypted aan Jetson-zijde. Rotatie via Setup-knop "Hercertificeer Jetson".

---

## 11. Setup-UI uitbreidingen

In Setup verschijnt een nieuwe tab "AI-masking" met de volgende cards:

| Card | Inhoud |
|---|---|
| Status | Huidige tier, detector-type, model-versie, laatste benchmark-resultaat |
| Hardware | Probe-output (CPU, GPU, NPU, remote-endpoints), "Probe opnieuw"-knop |
| Per-camera | Per camera aan/uit-toggle voor AI-mask, prioriteitsvolgorde voor degradatie, motion-gating-drempel |
| Klassen | Welke object-klassen actief zijn (plate, face, person), per klasse confidence-drempel |
| Fall-back | Tegel-status bij detector-uitval (altijd full-tile blur, niet configureerbaar in 2.0; reden uitgelegd in tekst) |
| Benchmark | "Benchmark opnieuw draaien"-knop met laatste resultaten |
| Jetson | Bij Baseline B: endpoint-adres, paring-status, certificaat-info, "Hercertificeer"-knop |

Bij tier 0 toont de tab alleen Status en Hardware met de boodschap "AI-features niet beschikbaar op deze host" en een verwijzing naar de hardware-baseline-documentatie.

Alle bestaande Setup-tabs (NVR, Cameras, Privacy, Audit, About) blijven ongewijzigd. AI-masking is een additie, geen herontwerp.

---

## 12. Reveal-flow

Reveal wordt afgehandeld via de bestaande PIN- of AD-policy uit v1.x. Geen aparte AI-reviewer-role in v2.0.

- Reviewer klikt op een tegel die AI-masking actief heeft.
- Display toont reveal-bevestiging met duur (bv. 30 s, 1 min, 5 min, tot expliciete re-mask).
- Bij bevestiging wordt voor de gekozen duur het AI-masking-overlay op die tegel uitgezet. De camera blijft zichtbaar zoals zonder mask.
- Audit-event `ai.reveal.requested` met reviewer-identiteit en duur.
- Bij expiratie auto-herstel van AI-masking. Audit-event `ai.reveal.expired`.
- Reviewer kan ook handmatig vroegtijdig re-masken.

**Belangrijk**: reveal lift het AI-masking-overlay, niet de individuele detecties. Granulaire reveal per detection (bv. "blur dit kenteken niet, de rest wel") is bewust niet in v2.0; te complex qua UX en juridisch risicovol.

---

## 13. Open punten

### Opgelost

| # | Onderwerp | Resolutie |
|---|---|---|
| ~~1~~ | IDIS GDK Linux ARM64-beschikbaarheid | Bevestigd 11 mei 2026, beschikbaar bij IDIS Nederland. Jetson krijgt native GDK-decode. |
| ~~2~~ | Detection-model strategie voor v2.0 | Pretrained YuNet (face) en YOLOv8n COCO (person / two-wheeler / vehicle) leveren vier van de vijf categorieën zonder eigen training. LicensePlate verschuift naar v2.0.x, in-house getraind via Roboflow. |

### Nog open

| # | Onderwerp | Owner |
|---|---|---|
| 3 | Model-distributie: in installer meeleveren, of bij first-run download met checksum-verificatie | Build / Setup |
| 4 | Reveal-flow autorisatie: hergebruik PIN/AD, of nieuwe rol "AI-reviewer" | Product (huidige voorstel: hergebruik) |
| 5 | Recording-side blurring (NVR-post-process) | Expliciet buiten v2.0-scope; mogelijk v3.0 |
| 6 | Licensering AI-module: aparte SKU/add-on, of standaard inbegrepen vanaf bepaalde tier | Sales / commercie |
| 7 | Dataset NL-platen voor v2.0.x finetuning: omvang, augmentation, privacy-aspecten van trainings-data | IDIS Nederland (in-house via Roboflow) |

---

## 14. Revisiehistorie

| Versie | Datum | Auteur | Wijziging |
|---|---|---|---|
| 0.1 | 2026-05-11 | Claude (concept) | Initiële opzet na retail-partner-aanvraag |
| 0.2 | 2026-05-11 | Claude | IDIS GDK ARM64 bevestigd beschikbaar; model-keuze beslist (in-house Roboflow). Open punten 1 en 2 gesloten. |
| 0.3 | 2026-05-11 | Claude | Sequencing vastgelegd: Windows-baseline eerst 100% productie-rijp (v2.0), Jetson ARM-port daarna (v2.1). Trainings-data komt uit in-house ANPR-archief (domein-correct materiaal). |
| 0.4 | 2026-05-11 | Claude | v2.0-scope verbreed naar multi-class (Face + Person + TwoWheeler + Vehicle) via pretrained models; LicensePlate verschoven naar v2.0.x. ObjectClass-enum + RawClassLabel toegevoegd. Drie-categorie-mapping (Person / TwoWheeler / Vehicle) als UI-grouping. |
| 0.5 | 2026-05-11 | Claude | Einde implementatie-sessie. Documenteert daadwerkelijke implementatie-keuzes: centralized `InferenceCoordinator` ipv per-tile concurrent submissions (was bron van crashes); face-detection dropped en vervangen door YOLOv8n COCO (kleine gezichten op security-camera afstand te onbetrouwbaar); per-camera AI-config (toggle, klassen, mask-padding, ROI polygon) gerealiseerd; ORT bumped 1.20.1 → 1.24.4 voor opset 22 support; YOLO11n drop-in voorbereid maar Ultralytics-export heeft DirectML EP kernel-gap (CPU-fallback op nieuwe ops → 6x slowdown), wachten op betere export. Audit-events `AiDetectorInit / Fault / Stopped` toegevoegd. |
