# Buildnote — InteractiveMask v2.0.0

**Build-datum:** 11 mei 2026
**Build-machine:** lokale dev-host (Windows 11 x64)
**Verspreidingsstatus:** ✅ klaar voor distributie

---

## Artefact

| | |
|---|---|
| **Bestand** | `InteractiveMask-2.0.0.msi` |
| **Locatie** | `E:\InteractiveMask\build\publish\InteractiveMask-2.0.0.msi` |
| **Grootte** | 180.8 MB (189.632.512 bytes) |
| **SHA-256** | `6D503F5D0C9838C5BBC41DF77FD22896F38E87ACCF3CCA6F94A066386FFB9DEC` |
| **Type** | Windows Installer (MSI), 64-bit, self-contained |
| **Major-upgrade** | Vervangt v1.x in-place; geen handmatige de-installatie vereist |

### Code-signing

| | |
|---|---|
| **Authenticode-status** | ✅ Valid |
| **Certificate** | `CN=IDIS Nederland BV, O=IDIS Nederland BV, S=Noord-Brabant, C=NL` |
| **Thumbprint** | `39F90C0C81B45F2FAE47677E623A7F5D79185A19` |
| **Cert-geldigheid** | tot 4 april 2027 |
| **File-digest** | SHA-256 |
| **Timestamp** | DigiCert SHA256 RSA4096 Timestamp Responder 2025 |
| **Gesigneerd** | 9 binaries (Display.exe + 5 DLL's, WebHost.exe + 2 DLL's) + de MSI zelf |

Verificatie aan kant van de eindgebruiker:

```powershell
Get-AuthenticodeSignature 'InteractiveMask-2.0.0.msi'
```

Verwachte output: `Status : Valid`, signer-CN `IDIS Nederland BV`.

---

## Wat zit erin

### Applicatie-componenten

- **Display.exe** — WPF kiosk-applicatie (de "voorkant" die de operators zien)
- **WebHost.exe** — ASP.NET Core service voor browser-bediening op afstand
- **IDIS GDK 6.6.1** native runtime (G2ClientGDK, G2LibavMT, G2FishEye, SDL2)
- **ONNX Runtime 1.24.4 + DirectML EP** voor lokale AI-inferentie op de GPU
- **.NET 9 + WindowsDesktop runtime** (self-contained — de host hoeft niets vooraf te installeren)

### AI-modellen

Geleverd in `%ProgramFiles%\InteractiveMask\Display\models\`:

| Model | Doel | Grootte |
|---|---|---|
| `yolo26s-seg.onnx` | Productie detection + segmentation (Person / TwoWheeler / Vehicle) | 41.9 MB |
| `yolo26n-seg.onnx` | Lichtere fallback-variant, zelfde categorieën | 10.7 MB |
| `mobilenetv2-7.onnx` | Benchmark-model voor host-capability-probe | 13.6 MB |

### Installatie-locaties

| Pad | Inhoud |
|---|---|
| `%ProgramFiles%\InteractiveMask\Display\` | Kiosk-applicatie |
| `%ProgramFiles%\InteractiveMask\Service\` | WebHost-service binaries |
| Windows service `InteractiveMaskWebHost` | Auto-start, LocalSystem account |
| Firewall-regel | TCP poort 8080 (WebHost browser-bediening) |
| Start-menu + Desktop shortcut | Verwijst naar Display.exe |
| `%PROGRAMDATA%\InteractiveMask\` | Configuratie, audit-log, persisted mask-state |

### Registry

| Key | Doel |
|---|---|
| `HKLM\Software\InteractiveMask\InitialLanguage` | Alleen gezet als de admin `INTERACTIVEMASK_LANGUAGE=de/en/fr/es/nl` meegeeft bij msiexec |
| `HKCU\Software\InteractiveMask\installed` | MSI bookkeeping |
| `HKCU\Software\InteractiveMask\desktop_shortcut` | MSI bookkeeping |

---

## Wat is nieuw t.o.v. v1.3.0

Volledige release notes: [`docs/release-notes-2.0.0.md`](release-notes-2.0.0.md). Hoofdpunten:

- **AI-gestuurde privacy-masking** via YOLO26s-seg op de GPU (DirectML).
  Drie categorieën: Person (rood), TwoWheeler (oranje), Vehicle (blauw).
- **Per-camera configuratie**: aan/uit, klasse-keuze, confidence-slider met
  hysteresis tegen flicker, mask-padding, ROI-polygon, mask-stijl (kleur-coded
  silhouet vs source-blur CCTV-look), mask-dekking 20-100 %.
- **Per-tegel AI-reveal flow** (F2): klein "AI"-pilletje linksboven elke tegel,
  na auth een duur-keuze (30 s / 1 min / 5 min / handmatig her-masken),
  countdown-badge rechtsboven, audit-event bij elk reveal.
- **Adaptive load management** (F3): runtime-monitor schaalt automatisch af
  bij GPU-overload (frame-skip → per-camera disable → global fallback) en
  herstelt automatisch zodra belasting daalt. Bezoekend bewustzijn via een
  oranje "AI gepauzeerd (belasting)" badge op betroffen tegels.
- **AI-instellingen dialog** volledig herontworpen in card-stijl.
- **Hoofdstuk 10 "AI-maskering"** toegevoegd aan de in-app handleiding (NL + EN).
- **About-tab** toont live AI-runtime info (model, execution provider,
  inference-latency, frame-counter).
- **34 commits** sinds v1.3.0 + alle pre-release polish uit v1.3 review afgewikkeld.

### Breaking changes

Geen — backward compatible. Een v1.3.x-configuratie laadt onveranderd; nieuwe
AI-velden vallen terug op hun defaults tot de operator ze in Setup aanpast.
Cameras zonder AI-config gedragen zich exact zoals in v1.3.0.

---

## Deployment

### Standaard (interactief)

Dubbelklikken op de MSI start de WiX installer-wizard met taal-keuze
(NL standaard), licentie-acceptatie, installatie-pad en optionele
"Start meteen na installatie"-vinkje.

### Unattended (SCCM / Intune / mass-rollout)

```cmd
msiexec /i InteractiveMask-2.0.0.msi /qn INTERACTIVEMASK_LANGUAGE=de
```

Vervang `de` door `nl`, `en`, `fr`, of `es`. Property weglaten = first-run
language picker bij eerste start.

### Pre-flight checklist endpoint

- Windows 10 22H2 of 11 23H2 / 24H2, x64
- DirectML-capable GPU (NVIDIA / Intel Arc / AMD; driver 2020 of nieuwer)
- 4 GB vrij op systeemschijf (180 MB MSI + ~700 MB unpacked + .NET runtime)
- LAN-toegang naar de IDIS NVR(s)
- Lokaal admin-account voor de installatie (vereist door MSI scope=perMachine)

### Verwachte runtime-eigenschappen

| | |
|---|---|
| Cold start | ~3-5 s tot live video op een 4×4-grid |
| AI inference latency | 25-35 ms per frame op mid-range dGPU (RTX 4060) |
| Steady-state CPU | ~5-10 % op een 16-tegel grid |
| Steady-state GPU | ~30-50 % afhankelijk van model-variant + aantal AI-cameras |
| RAM | ~600-900 MB |

---

## Bekende beperkingen

1. **LicensePlate-categorie** nog niet uitgerold — wacht op in-house Roboflow
   getraind model (gepland als v2.0.x patch). `ObjectClass.LicensePlate` enum
   staat klaar.
2. **GPU verplicht** voor AI-masking. Op een host zonder DirectML-capable
   GPU werkt v1.x manuele masking gewoon; AI-features zijn niet beschikbaar.
3. **Jetson Orin Nano ARM64 sidecar** is gepland voor v2.1. v2.0 is uitsluitend
   in-process Windows + dGPU.
4. **Help-content in DE / FR / ES** valt nog terug op EN. Vertaalwerk staat
   op de roadmap; technische DE/FR/ES UI-elementen zijn wel volledig vertaald.

---

## Commit-trail t.o.v. v1.3.0

34 commits op `main`, vastgelegd in tag `v2.0.0` (commit `fe2cff1`).
De build die in deze MSI zit is gebouwd vanaf commit `3300bd7` (post-tag
installer-fix om de modelfiles in de publish-output te krijgen).

Belangrijkste merkpunten:

```
3300bd7 fix(installer): include AI detector models in Display publish output
5c8f8c1 docs(help): chapter 10 - AI-masking + reveal + adaptive load
d6d3566 feat(degradation): adaptive AI load manager with hysteresis (F3)
9b6df21 feat(reveal): per-tile AI-reveal flow with auth, countdown badge (F2)
f0a16b6 feat(rendering): per-camera mask style toggle + opacity slider
f9f3d9b feat(detection): YOLO26s-seg baseline + confidence slider + hysteresis
d9c9987 feat(rendering): colour-coded class silhouettes + radius-40 privacy blur
96c581e feat(detection): segmentation masking via YOLO26n-seg
c4824d7 refactor(detection): centralized inference coordinator
bcd0f9a feat(detection): YOLOv8n COCO multi-class detection + real region-blur
fe2cff1 v2.0.0: AI-driven privacy masking + UI overhaul       ← tagged v2.0.0
```

Volledige geschiedenis: `git log v1.3.0..v2.0.0 --oneline` op de repo.

---

## Smoke-test aanbevolen voor verzending

Bij voorkeur op een schone Windows 11 VM:

1. Installeer de MSI (interactieve modus).
2. Vink "Start meteen na installatie" aan op de finish-dialog.
3. Controleer: Display window opent, About-tab toont `v2.0.0`.
4. Configureer één NVR + één camera in Setup.
5. Open AI… op die camera, zet AI aan, klik Save.
6. Verifieer in de live tegel dat detecties verschijnen met gekleurde silhouetten.
7. Klik op het AI-pilletje linksboven → reveal-flow doorlopen.
8. Verifieer audit-log: `%PROGRAMDATA%\InteractiveMask\audit.log` bevat
   `AiDetectorInit`, `AiRevealRequested`, `AiRevealExpired` regels.
9. Open de in-app handleiding (Setup → Administrator → Open manual) en
   bevestig dat hoofdstuk 10 "AI-maskering" zichtbaar en leesbaar is.
10. Re-boot, controleer dat AI-config en privacy-state behouden blijven.

---

## Contact / support

| | |
|---|---|
| **Support email** | support@bnl.idisglobal.com |
| **Issues / bugs** | GitHub Issues op de project-repo |
| **Documentatie** | `docs/release-notes-2.0.0.md`, `docs/architecture-v2-ai.md`, `docs/roadmap.md` |

---

*Buildnote opgesteld door Claude (Sonnet 4.5) als onderdeel van de v2.0.0 release-sessie op 11 mei 2026.*
