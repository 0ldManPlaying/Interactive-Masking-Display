// Builds docs/release-notes-2.0.0.docx from the v2.0.0 release-notes content.
// Mirrors the layout, design tokens and helpers of build-release-notes-docx.js
// (the v1.3.0 generator) so the two PDFs / Word files look like a series.
// One-shot script; run with: node build-release-notes-2.0.0-docx.js

const fs = require('fs');
const path = require('path');
const {
    Document, Packer, Paragraph, TextRun,
    Table, TableRow, TableCell,
    Header, Footer, AlignmentType,
    LevelFormat, TabStopType, TabStopPosition,
    HeadingLevel, BorderStyle, WidthType, ShadingType,
    VerticalAlign, PageNumber, PageBreak,
    TableOfContents,
} = require('docx');

const OUTPUT = path.join(__dirname, 'release-notes-2.0.0.docx');

// ----- design tokens (identical to v1.3.0) -----------------------------------

const FONT_BODY = "Calibri";
const FONT_MONO = "Consolas";

const BODY_SIZE = 22;       // half-points (11pt)
const TABLE_SIZE = 20;      // 10pt
const SMALL_SIZE = 18;      // 9pt
const TITLE_SIZE = 56;      // 28pt
const SUBTITLE_SIZE = 30;   // 15pt
const H1_SIZE = 32;         // 16pt
const H2_SIZE = 28;         // 14pt
const H3_SIZE = 24;         // 12pt

const COLOR_TEXT = "1F2937";
const COLOR_MUTED = "4B5563";
const COLOR_ACCENT = "1F4E79";
const COLOR_HEADER_FILL = "1F4E79";
const COLOR_HEADER_TEXT = "FFFFFF";
const COLOR_BORDER = "BFBFBF";
const COLOR_CODE_FILL = "F3F4F6";

// A4 with 1-inch margins
const PAGE_WIDTH = 11906;
const PAGE_HEIGHT = 16838;
const PAGE_MARGIN = 1440;

// ----- run helpers -----------------------------------------------------------

function r(text, opts = {}) {
    return new TextRun({
        text,
        font: opts.font || FONT_BODY,
        size: opts.size || BODY_SIZE,
        color: opts.color || COLOR_TEXT,
        bold: opts.bold,
        italics: opts.italics,
    });
}

function code(text) {
    return new TextRun({
        text,
        font: FONT_MONO,
        size: BODY_SIZE - 2,
        shading: { type: ShadingType.CLEAR, fill: COLOR_CODE_FILL, color: "auto" },
    });
}

function bold(text) { return r(text, { bold: true }); }
function italic(text) { return r(text, { italics: true }); }

// ----- block helpers ---------------------------------------------------------

function para(runs, opts = {}) {
    if (typeof runs === 'string') runs = [r(runs)];
    if (!Array.isArray(runs)) runs = [runs];
    return new Paragraph({
        spacing: { after: 120, ...(opts.spacing || {}) },
        ...opts,
        children: runs,
    });
}

function codeBlock(text) {
    return new Paragraph({
        spacing: { before: 100, after: 200 },
        shading: { type: ShadingType.CLEAR, fill: COLOR_CODE_FILL, color: "auto" },
        children: [new TextRun({ text, font: FONT_MONO, size: BODY_SIZE - 2 })],
    });
}

function h1(text) {
    return new Paragraph({
        heading: HeadingLevel.HEADING_1,
        children: [r(text, { bold: true, size: H1_SIZE, color: COLOR_ACCENT })],
    });
}
function h2(text) {
    return new Paragraph({
        heading: HeadingLevel.HEADING_2,
        children: [r(text, { bold: true, size: H2_SIZE, color: COLOR_ACCENT })],
    });
}
function h3(text) {
    return new Paragraph({
        heading: HeadingLevel.HEADING_3,
        children: [r(text, { bold: true, size: H3_SIZE })],
    });
}

function bullet(runs) {
    if (typeof runs === 'string') runs = [r(runs)];
    if (!Array.isArray(runs)) runs = [runs];
    return new Paragraph({
        numbering: { reference: "bullets", level: 0 },
        spacing: { after: 80 },
        children: runs,
    });
}

function num(runs) {
    if (typeof runs === 'string') runs = [r(runs)];
    if (!Array.isArray(runs)) runs = [runs];
    return new Paragraph({
        numbering: { reference: "numbers", level: 0 },
        spacing: { after: 80 },
        children: runs,
    });
}

// ----- table helpers ---------------------------------------------------------

const cellBorder = { style: BorderStyle.SINGLE, size: 4, color: COLOR_BORDER };
const cellBorders = { top: cellBorder, bottom: cellBorder, left: cellBorder, right: cellBorder };
const cellMargins = { top: 80, bottom: 80, left: 120, right: 120 };

function thCell(text, widthDxa) {
    return new TableCell({
        borders: cellBorders,
        margins: cellMargins,
        width: { size: widthDxa, type: WidthType.DXA },
        shading: { type: ShadingType.CLEAR, fill: COLOR_HEADER_FILL, color: "auto" },
        verticalAlign: VerticalAlign.CENTER,
        children: [new Paragraph({
            children: [r(text, { bold: true, color: COLOR_HEADER_TEXT, size: TABLE_SIZE })],
        })],
    });
}

function tdCell(content, widthDxa) {
    let runs;
    if (typeof content === 'string') {
        runs = [r(content, { size: TABLE_SIZE })];
    } else if (Array.isArray(content)) {
        runs = content;
    } else {
        runs = [content];
    }
    return new TableCell({
        borders: cellBorders,
        margins: cellMargins,
        width: { size: widthDxa, type: WidthType.DXA },
        verticalAlign: VerticalAlign.TOP,
        children: [new Paragraph({ children: runs })],
    });
}

function buildTable(widths, headerLabels, rows) {
    const total = widths.reduce((a, b) => a + b, 0);
    return new Table({
        width: { size: total, type: WidthType.DXA },
        columnWidths: widths,
        rows: [
            new TableRow({
                tableHeader: true,
                children: headerLabels.map((label, i) => thCell(label, widths[i])),
            }),
            ...rows.map(row => new TableRow({
                children: row.map((cell, i) => tdCell(cell, widths[i])),
            })),
        ],
    });
}

// ----- title page ------------------------------------------------------------

const titlePage = [
    new Paragraph({ spacing: { before: 3000 }, children: [] }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { after: 200 },
        children: [r("InteractiveMask v2.0.0", { bold: true, size: TITLE_SIZE, color: COLOR_ACCENT })],
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { after: 800 },
        children: [r("Release Notes", { bold: true, size: SUBTITLE_SIZE })],
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { after: 100 },
        children: [r("Released 11 May 2026", { size: 26, color: COLOR_MUTED })],
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { after: 4000 },
        children: [r("IDIS Nederland BV", { bold: true, size: 26 })],
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        children: [r("Proprietary — IDIS Nederland BV. All rights reserved.",
            { italics: true, size: SMALL_SIZE, color: COLOR_MUTED })],
    }),
    new Paragraph({ children: [new PageBreak()] }),
];

// ----- header / footer -------------------------------------------------------

const docHeader = new Header({
    children: [new Paragraph({
        spacing: { after: 0 },
        border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: COLOR_ACCENT, space: 6 } },
        children: [r("InteractiveMask v2.0.0 Release Notes — IDIS Nederland BV",
            { size: SMALL_SIZE, color: COLOR_MUTED })],
    })],
});

const docFooter = new Footer({
    children: [new Paragraph({
        spacing: { before: 0 },
        tabStops: [{ type: TabStopType.RIGHT, position: TabStopPosition.MAX }],
        children: [
            r("Proprietary — IDIS Nederland BV. All rights reserved.", { size: SMALL_SIZE, color: COLOR_MUTED }),
            new TextRun({ text: "\t", size: SMALL_SIZE }),
            r("Page ", { size: SMALL_SIZE, color: COLOR_MUTED }),
            new TextRun({ children: [PageNumber.CURRENT], size: SMALL_SIZE, color: COLOR_MUTED, font: FONT_BODY }),
            r(" of ", { size: SMALL_SIZE, color: COLOR_MUTED }),
            new TextRun({ children: [PageNumber.TOTAL_PAGES], size: SMALL_SIZE, color: COLOR_MUTED, font: FONT_BODY }),
        ],
    })],
});

// ----- body ------------------------------------------------------------------

const body = [];

body.push(h1("InteractiveMask v2.0.0 — Release Notes"));

body.push(buildTable([2700, 6326],
    ["Field", "Value"],
    [
        ["Release date", "11 May 2026"],
        ["Build", [r("InteractiveMask-2.0.0.msi", { size: TABLE_SIZE }), r(" (180.8 MB, self-contained)", { size: TABLE_SIZE })]],
        ["Signed by", "IDIS Nederland BV (Authenticode, SHA-256, DigiCert SHA256 RSA4096 timestamp; certificate valid until 4 April 2027)"],
        ["SHA-256", [code("6D503F5D0C9838C5BBC41DF77FD22896F38E87ACCF3CCA6F94A066386FFB9DEC")]],
        ["Compatibility", "Windows 10 22H2 / 11 23H2 / 11 24H2 x64, DirectML-capable GPU recommended for AI features"],
        ["Upgrade path", [r("In-place over v1.x — existing ", { size: TABLE_SIZE }), code("config.json"), r(" migrates automatically. New AI fields take their defaults until configured.", { size: TABLE_SIZE })]],
    ],
));
body.push(para([], { spacing: { after: 200 } }));

body.push(h1("Table of contents"));
body.push(new TableOfContents("Table of Contents", { hyperlink: true, headingStyleRange: "1-3" }));
body.push(new Paragraph({ children: [new PageBreak()] }));

// ===== Executive summary =====
body.push(h1("Executive summary"));
body.push(para([
    r("v2.0 introduces "),
    bold("AI-driven object masking"),
    r(" as the headline feature. A local AI model recognises people, two-wheelers and vehicles in the camera frame and applies privacy masks per detected object rather than blurring the entire tile. The system is fully GDPR-compliant: no facial recognition, no licence-plate reading, no external database links, no plate-string storage. Customers without a suitable GPU keep the v1.x masking behaviour unchanged; AI is strictly opt-in per camera and the new configuration falls back to safe defaults on hosts that cannot run inference."),
]));
body.push(para([
    r("This is a major release. The detection pipeline, rendering pipeline, persistence schema and AI settings UI are all new. Existing v1.3.x installations upgrade in place; configuration is preserved unchanged, and uncongifured cameras behave identically to v1.3.0."),
]));

// ===== Headline features =====
body.push(h1("Headline features"));

// 1. AI-driven privacy masking
body.push(h2("1. AI-driven privacy masking"));
body.push(para([
    r("YOLO26s-seg segmentation runs locally on the GPU through ONNX Runtime DirectML. The detector recognises three categories, each rendered in a distinct colour on the live tile:"),
]));
body.push(bullet([bold("Person"), r(" — red ("), code("#E74C3C"), r(")")]));
body.push(bullet([bold("TwoWheeler"), r(" — orange ("), code("#F39C12"), r("), covering bicycles and motorcycles")]));
body.push(bullet([bold("Vehicle"), r(" — blue ("), code("#3498DB"), r("), covering cars, vans, buses and trucks")]));
body.push(para([
    r("A "),
    bold("centralised InferenceCoordinator"),
    r(" owns the single ONNX session and serialises every inference call through one background worker with slot-replacement semantics. Newer frames overwrite older pending ones rather than queueing up; the previous prototype's native crashes from concurrent ORT calls are eliminated."),
]));
body.push(para([
    r("Target inference latency on a mid-range Windows GPU (e.g. NVIDIA RTX 4060): 25–35 ms per frame. The system runs comfortably on a 4×4 grid with 4–8 AI-enabled cameras at steady-state GPU utilisation around 30–50 %."),
]));

// 2. Per-camera configuration
body.push(h2("2. Per-camera configuration"));
body.push(para([
    r("Operators decide per camera independently which categories get masked. Cameras with AI disabled run the v1.x full-tile manual-blur path with zero AI overhead. The new "),
    bold("AI Settings"),
    r(" dialog (Setup → Cameras → AI… button on a camera row) groups the per-camera options into card-style sections:"),
]));
body.push(bullet([bold("Enable AI masking"), r(" — master toggle for this camera.")]));
body.push(bullet([bold("Categories"), r(" — colour-coded pill buttons; each chip lights up in the class colour when checked.")]));
body.push(bullet([
    bold("Detection threshold"), r(" — slider from 15 % to 70 % (default 40 %). The threshold has "),
    bold("hysteresis"),
    r(" baked in: an object remains rendered down to half the slider value once locked on, with IoU ≥ 0.4 matching to the previous frame. This eliminates the on/off flicker on borderline detections at distance."),
]));
body.push(bullet([
    bold("Mask padding"), r(" — slider 0 % to 50 %. Enlarges the mask region around each detection to compensate for tight bounding boxes that don't fully cover the object."),
]));
body.push(bullet([
    bold("Mask style"), r(" — toggle between "),
    bold("colour-coded silhouette"),
    r(" (solid per-class colour clipped by the segmentation mask, then Gaussian-blurred) and "),
    bold("source blur"),
    r(" (the camera area itself sampled through a Gaussian blur — the classic CCTV redaction look)."),
]));
body.push(bullet([
    bold("Mask opacity"), r(" — slider 20 % to 100 %. Lets background context show through when the operator needs to see "), italic("that"), r(" a person is present without seeing "), italic("who"), r(" they are."),
]));
body.push(bullet([
    bold("Region of Interest"), r(" — polygon editor that opens on a live snapshot. Click to add vertices, drag existing vertices to fine-tune; detections whose bounding-box centroid falls outside the polygon are not masked. Skip the polygon entirely to use the whole camera."),
]));

// 3. Per-tile AI reveal flow
body.push(h2("3. Per-tile AI reveal flow"));
body.push(para([
    r("Authorised reviewers can temporarily lift the AI overlay on one tile when an incident genuinely requires identifying a person or vehicle. The flow reuses the existing v1.x PIN / Active-Directory authentication policy non-consumingly (the session PIN is verified but not consumed)."),
]));
body.push(para([
    r("Trigger: a small "),
    bold("AI"),
    r(" pill in the top-left of every tile with active AI detections. Visible only when AI is enabled on the camera, the tile is not fully manually masked, and no reveal is already active."),
]));
body.push(para([bold("Reveal duration options:")]));
body.push(num("30 seconds"));
body.push(num("1 minute"));
body.push(num("5 minutes"));
body.push(num("Until the reviewer presses the on-tile remask badge"));
body.push(para([
    r("During an active reveal the AI overlay is suppressed and a live countdown badge appears in the top-right: "),
    italic("\"AI off · 0:23\""),
    r(", ticking down per second. One click on the badge force-ends the reveal early (audit reason: "),
    code("manual-remask"),
    r(")."),
]));
body.push(para([bold("Privacy boundaries:")]));
body.push(bullet([r("Per-detection reveal ("), italic("blur this plate but not the other one"), r(") is intentionally "), bold("not"), r(" supported. Reveal lifts the AI overlay on the whole tile only.")]));
body.push(bullet("NVR recordings are unaffected; the reveal is a screen-side action only."));
body.push(bullet([r("Every reveal writes an "), code("AiRevealRequested"), r(" audit event with reviewer identity, duration and camera; every termination writes "), code("AiRevealExpired"), r(" with the reason.")]));
body.push(bullet("Mass-mask, mass-unmask, a manual full-tile mask, or a Setup-Apply that rebinds the camera all cancel active reveals immediately. The audit row captures which event ended the reveal."));

// 4. Adaptive load management
body.push(h2("4. Adaptive load management"));
body.push(para([
    r("A runtime monitor watches the rolling p95 inference latency reported by every completed detection and walks a three-step degradation ladder when the detector is consistently behind its budget. Steps recover (in reverse order) when latency drops back below the restore threshold. "),
    bold("Hysteresis"),
    r(" — separate degrade and restore thresholds plus N consecutive samples required to step in either direction — prevents flapping on borderline loads."),
]));
body.push(para([bold("Ladder (escalate; reverse on recover):")]));
body.push(num([bold("FrameSkip"), r(" — per-tile cadence multiplier doubles (every 3rd → 6th → 12th frame), up to a ceiling of ×8.")]));
body.push(num([bold("PerCameraDisable"), r(" — highest stream ID is auto-disabled. The render pipeline collapses the AI overlay on that tile and a small "), italic("\"AI paused (load)\""), r(" badge appears in the top-left. One more stream per escalate step.")]));
body.push(num([bold("GlobalFallback"), r(" — every eligible stream disabled. Tiles continue to show the live image without overlay; manual full-tile masking from v1.x is unaffected.")]));
body.push(para([
    r("Each transition writes an "),
    code("AiDetectorDegraded"),
    r(" or "),
    code("AiDetectorRestored"),
    r(" audit event with the from / to state, frame-skip multiplier and p95 latency. The operator sees the on-tile badge but takes no action; recovery is fully automatic."),
]));

// 5. Documentation + about
body.push(h2("5. In-app help and About-tab telemetry"));
body.push(para([
    r("The in-app manual gains "),
    bold("Chapter 10 — AI masking"),
    r(", a six-section operator-facing walkthrough covering what AI masking does, per-camera setup, the two rendering styles, mask opacity, the ROI polygon, the reveal flow, where to find audit events, and the adaptive load management. Available in NL and EN (DE / FR / ES fall back to EN, consistent with the rest of the help content)."),
]));
body.push(para([
    r("The About tab now shows live AI runtime info: model name, execution provider (DirectML or CPU fallback), average inference latency and frame counter. The operator can verify at a glance that the AI pipeline is actually on the GPU and not silently falling back to CPU."),
]));

// ===== Foundational improvements =====
body.push(h1("Foundational improvements"));

body.push(h3("Dependency bump: ONNX Runtime DirectML 1.20.1 → 1.24.4"));
body.push(para("Required for YOLO26 (opset 22) model loading. Brings improved DirectML kernel coverage for the prototype-mask combine path."));

body.push(h3("Centralised inference coordinator"));
body.push(para("Replaces the per-tile concurrent submission pattern from earlier prototypes. Single worker, single ONNX session, channel-based wake-up with slot-replacement. Solves the native ORT crashes that occurred under concurrent inference attempts and bounds the GPU queue depth automatically."));

body.push(h3("Pre-release polish from the v1.3 review"));
body.push(para("Five carry-over items from the v1.3 pre-release engineering pass landed in v2.0:"));
body.push(bullet([r("Dead localised strings ("), code("NavGrid"), r(", "), code("GridPageTitle"), r(", "), code("AdminLangNl"), r(", "), code("AdminLangEn"), r(", "), code("StatusError"), r(") removed across all five languages.")]));
body.push(bullet([code("FetchCameraNamesAsync"), r(" now differentiates between external cancellation ("), code("TaskCanceledException"), r(") and an internal deadline ("), code("TimeoutException"), r(").")]));
body.push(bullet([r("Hard-coded Dutch error string in the Help-open path replaced with a proper localisation key ("), code("HelpOpenErrorFormat"), r(").")]));
body.push(bullet([code("PrimePrivacyDefaultState"), r(" now bumps the PIN-service masked-tile counter once per primed tile. The prior behaviour left the counter at zero while N tiles were visibly masked, which would clear the session PIN prematurely on the first reveal — a privacy regression.")]));
body.push(bullet([code("WebAccessSessionStore"), r(" sweeps expired tokens every 15 minutes via a background timer. The prior store only removed an expired entry when its specific token was re-presented; tokens whose client never came back lingered indefinitely.")]));

body.push(h3("Hot-path allocation cleanup"));
body.push(para([
    r("Three measurable improvements: ("),
    code("ApplyCameraChangesLive"),
    r(") now skips tiles whose configuration is unchanged, eliminating spurious "),
    code("UpdateLabel"),
    r(" notifications and "),
    code("AiClasses"),
    r(" / "),
    code("AiRoiPolygon"),
    r(" collection rebinds on every Setup → Apply. ("),
    code("AuditLog.Write"),
    r(") uses a persistent "),
    code("StreamWriter"),
    r(" instead of re-opening the audit file per event, while keeping the per-event flush for durability. ("),
    code("TileViewModel.OnFrameDecoded"),
    r(") uses cached "),
    code("Action<>"),
    r(" delegates instead of fresh closures per "),
    code("Dispatcher.BeginInvoke"),
    r(" — eliminates ~400 closure + display-class allocations per second on the GDK callback thread."),
]));

// ===== Audit-event additions =====
body.push(h1("Audit-event additions"));
body.push(para([
    r("The existing v1.x audit-event schema is unchanged. The following AI-related event types are added in v2.0; everything else is identical, so existing log-parsers and SIEM rules continue to apply."),
]));
body.push(buildTable([2200, 1100, 5726],
    ["Event type", "Status", "Trigger"],
    [
        [code("AiDetectorInit"),       "v2.0.0",   "Detector finished initialising and reached Ready. One per session."],
        [code("AiDetectorFault"),      "v2.0.0",   "Init failure or non-recoverable runtime fault; Display continues with v1.x masking only."],
        [code("AiDetectorStopped"),    "v2.0.0",   "Graceful detector shutdown on app close."],
        [code("AiRevealRequested"),    "v2.0.0",   "Authorised reviewer suppressed the AI overlay on one tile. Detail carries duration; source carries reviewer identity."],
        [code("AiRevealExpired"),      "v2.0.0",   "Reveal window ended. Detail reason: timer-expired / manual-remask / mass-mask / full-tile-mask / tile-rebound / ai-disabled."],
        [code("AiDetectorDegraded"),   "v2.0.0",   "Adaptive load manager escalated one step. Detail: from→to state, multiplier change, p95 latency."],
        [code("AiDetectorRestored"),   "v2.0.0",   "Adaptive load manager recovered one step. Same detail shape."],
    ],
));
body.push(para([
    r("Important boundary: "),
    bold("no detection coordinates, no plate content and no frame data appear in the audit log."),
    r(" Only metadata about detector state and reviewer actions. This keeps the audit file free of personally-identifying material and consistent with the no-ANPR / no-OCR principle."),
]));

// ===== Backward compatibility =====
body.push(h1("Backward compatibility and migration"));
body.push(para("v2.0.0 is binary- and configuration-compatible with v1.3.x. A v1.3.x install upgrades in place; on first launch after upgrade the behaviour is identical to v1.3.0 (AI is off on every camera until the operator opts in)."));

body.push(h3("Configuration schema additions"));
body.push(para([
    r("Six new fields per camera in "),
    code("CameraSlotSettings"),
    r(" (persisted in "),
    code("config.json"),
    r("):"),
]));
body.push(buildTable([2600, 1600, 1500, 2326],
    ["Field", "Type", "Default", "Range"],
    [
        [code("AiEnabled"),               "bool",   "false",       "—"],
        [code("AiClasses"),               "string[]","[]",         "Person / TwoWheeler / Vehicle"],
        [code("AiConfidencePercent"),     "int",    "40",          "15–70"],
        [code("MaskPaddingPercent"),      "int",    "0",           "0–50"],
        [code("AiRoiPolygon"),            "PolygonPoint[]","[]",   "—"],
        [code("AiUseSourceBlur"),         "bool",   "false",       "—"],
        [code("AiMaskOpacityPercent"),    "int",    "100",         "20–100"],
    ],
));
body.push(para("Older config files load unchanged; missing fields take the defaults above until the operator opens AI Settings and saves."));

body.push(h3("Default behaviour on a fresh install"));
body.push(para("If no AI configuration is present, behaviour is identical to v1.3.0 — manual full-tile masks driven by operator taps, no model load, no GPU work. AI is strictly opt-in per camera."));

body.push(h3("Threading model"));
body.push(para([
    r("The detection pipeline is now driven by a single "),
    code("InferenceCoordinator"),
    r(" per camera rather than ad-hoc background tasks. Custom integrations from earlier prototypes that called the detection layer directly need to route through the coordinator's "),
    code("EnqueueFrame()"),
    r(" — multiple concurrent ORT calls into the same session are not supported and will crash the native layer."),
]));

// ===== Acceptance tests =====
body.push(h1("Acceptance tests (v2.0 release gate)"));
body.push(buildTable([3500, 5526],
    ["Test", "Expected outcome"],
    [
        ["Fresh install with no AI configured",                                      "Behaves exactly like v1.3.0; no model load on startup."],
        ["Enable AI on one camera with default settings",                            "Person / Vehicle silhouettes appear in the live tile within 5 seconds."],
        ["Toggle mask style colour-coded → source blur",                             "Live tile switches visual style without restart."],
        ["Adjust opacity 100 % → 50 %",                                              "Mask becomes visibly translucent; background bleeds through."],
        ["Draw an ROI polygon restricting detection to a doorway",                   "Detections outside the polygon are not masked."],
        ["Open About tab while AI is running",                                       "Shows non-zero inference latency and frame counter."],
        ["Restart kiosk",                                                            "All per-camera AI settings persist exactly as configured."],
        ["Click AI pill, authenticate, pick 1 minute",                               "AI overlay collapses on that tile; countdown badge appears top-right."],
        ["Wait for the timer to expire",                                             "AI overlay returns automatically; audit row written."],
        ["Long-press (mass mask) while a reveal is active",                          "Reveal cancels immediately; audit reason mass-mask."],
        ["Stress test: enable AI on 16 tiles simultaneously",                        "Adaptive controller engages; lowest-priority tiles get the load badge; system recovers when load drops."],
    ],
));

// ===== Installation =====
body.push(h1("Installation"));
body.push(h3("Standard (interactive)"));
body.push(para("Double-clicking the MSI launches the WiX installer wizard with language selection, EULA acceptance, install path and the optional \"Launch InteractiveMask after install\" tickbox on the finish dialog."));

body.push(h3("Unattended (SCCM / Intune / mass-rollout)"));
body.push(codeBlock("msiexec /i InteractiveMask-2.0.0.msi /qn INTERACTIVEMASK_LANGUAGE=en"));
body.push(para([
    r("Replace "),
    code("en"),
    r(" with "),
    code("nl"),
    r(", "),
    code("de"),
    r(", "),
    code("fr"),
    r(" or "),
    code("es"),
    r(". Omit the property entirely to keep the first-run language picker."),
]));

body.push(h3("Pre-flight checklist for the endpoint"));
body.push(bullet("Windows 10 22H2 or 11 23H2 / 24H2, x64"));
body.push(bullet("DirectML-capable GPU (NVIDIA RTX, AMD 700-series, Intel Arc; driver from 2020 or newer)"));
body.push(bullet("4 GB free on the system drive (180 MB MSI + ~700 MB unpacked + .NET runtime)"));
body.push(bullet("LAN access to the IDIS NVR(s)"));
body.push(bullet("Local administrator account for the installation (the MSI runs at perMachine scope)"));

// ===== Known limitations =====
body.push(h1("Known limitations"));
body.push(bullet([bold("LicensePlate category"), r(" — in-house Roboflow-trained model is in progress; the "), code("ObjectClass.LicensePlate"), r(" enum value is reserved. Will ship as a v2.0.x patch.")]));
body.push(bullet([bold("GPU required"), r(" — AI masking needs a DirectML-capable GPU. A CPU fallback exists but is too slow for live use (≤ 5 fps); operators should leave AI off on machines without a suitable GPU.")]));
body.push(bullet([bold("Three-class taxonomy"), r(" — only Person, TwoWheeler and Vehicle are exposed today. Additional categories (face-only, license plate, etc.) are v2.0.x / v2.1 candidates.")]));
body.push(bullet([bold("No NVR-side recording integration"), r(" — masks are applied to the live display only; the NVR continues to record the unmasked stream as configured on the recorder.")]));
body.push(bullet([bold("Jetson Orin Nano ARM64 sidecar"), r(" — planned for v2.1; v2.0 is Windows + dGPU only.")]));
body.push(bullet([bold("Help content"), r(" in DE / FR / ES falls back to EN; UI elements are fully translated in all five languages.")]));

// ===== Upgrade path =====
body.push(h1("Upgrade path from v1.3.x"));
body.push(num("Install the v2.0.0 MSI over the existing v1.3.x installation. Configuration and audit log are preserved."));
body.push(num("On first launch, behaviour is identical to v1.3.0. AI features are visible but disabled per camera by default."));
body.push(num("Open Setup → Cameras → AI… on any camera row to opt in per camera."));
body.push(para("No data migration is required. The persisted state, audit log, and admin PIN all read forward unchanged."));

// ===== Credits =====
body.push(h1("Credits"));
body.push(bullet([bold("Model architecture: "), r("YOLO26s-seg by Ultralytics (AGPL-3.0 licence).")]));
body.push(bullet([bold("Inference runtime: "), r("Microsoft ONNX Runtime with the DirectML execution provider.")]));
body.push(bullet([bold("Camera SDK: "), r("IDIS GDK 6.6.1 by IDIS Co., Ltd.")]));
body.push(bullet([bold("Release engineering and acceptance testing: "), r("IDIS Nederland BV.")]));

// ----- doc -------------------------------------------------------------------

const doc = new Document({
    creator: "IDIS Nederland BV",
    title: "InteractiveMask v2.0.0 Release Notes",
    description: "Release notes for InteractiveMask v2.0.0",
    styles: {
        default: {
            document: { run: { font: FONT_BODY, size: BODY_SIZE, color: COLOR_TEXT } },
        },
        paragraphStyles: [
            {
                id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
                run: { size: H1_SIZE, bold: true, color: COLOR_ACCENT, font: FONT_BODY },
                paragraph: { spacing: { before: 320, after: 160 }, outlineLevel: 0 },
            },
            {
                id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
                run: { size: H2_SIZE, bold: true, color: COLOR_ACCENT, font: FONT_BODY },
                paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1 },
            },
            {
                id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
                run: { size: H3_SIZE, bold: true, color: COLOR_TEXT, font: FONT_BODY },
                paragraph: { spacing: { before: 200, after: 100 }, outlineLevel: 2 },
            },
        ],
    },
    numbering: {
        config: [
            {
                reference: "bullets",
                levels: [{
                    level: 0, format: LevelFormat.BULLET, text: "•",
                    alignment: AlignmentType.LEFT,
                    style: { paragraph: { indent: { left: 720, hanging: 360 } } },
                }],
            },
            {
                reference: "numbers",
                levels: [{
                    level: 0, format: LevelFormat.DECIMAL, text: "%1.",
                    alignment: AlignmentType.LEFT,
                    style: { paragraph: { indent: { left: 720, hanging: 360 } } },
                }],
            },
        ],
    },
    sections: [
        {
            properties: {
                page: {
                    size: { width: PAGE_WIDTH, height: PAGE_HEIGHT },
                    margin: { top: PAGE_MARGIN, right: PAGE_MARGIN, bottom: PAGE_MARGIN, left: PAGE_MARGIN },
                },
            },
            children: titlePage,
        },
        {
            properties: {
                page: {
                    size: { width: PAGE_WIDTH, height: PAGE_HEIGHT },
                    margin: { top: PAGE_MARGIN, right: PAGE_MARGIN, bottom: PAGE_MARGIN, left: PAGE_MARGIN },
                },
            },
            headers: { default: docHeader },
            footers: { default: docFooter },
            children: body,
        },
    ],
});

Packer.toBuffer(doc).then(buf => {
    fs.writeFileSync(OUTPUT, buf);
    console.log("wrote " + OUTPUT + " (" + buf.length + " bytes)");
}).catch(e => {
    console.error("docx build failed", e);
    process.exit(1);
});
