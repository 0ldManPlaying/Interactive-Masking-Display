// Builds docs/release-notes-1.3.0.docx from the matching markdown source.
// One-shot script; run with: node build-release-notes-docx.js

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

const OUTPUT = path.join(__dirname, 'release-notes-1.3.0.docx');

// ----- design tokens ---------------------------------------------------------

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
        children: [r("InteractiveMask v1.3.0", { bold: true, size: TITLE_SIZE, color: COLOR_ACCENT })],
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { after: 800 },
        children: [r("Release Notes", { bold: true, size: SUBTITLE_SIZE })],
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { after: 100 },
        children: [r("Released 8 May 2026", { size: 26, color: COLOR_MUTED })],
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
        children: [r("InteractiveMask v1.3.0 Release Notes — IDIS Nederland BV",
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

body.push(h1("InteractiveMask v1.3.0 — Release Notes"));

body.push(buildTable([2700, 6326],
    ["Field", "Value"],
    [
        ["Release date", "8 May 2026"],
        ["Build", [r("InteractiveMask-1.3.0.msi", { size: TABLE_SIZE }), r(" (109 MB, self-contained)", { size: TABLE_SIZE })]],
        ["Signed by", "IDIS Nederland BV (EV Authenticode, Sectigo Public Code Signing CA R36, SHA-256 + DigiCert timestamp)"],
        ["Compatibility", "Windows 10/11 x64, requires IDIS GDK 6.6.1 native runtime alongside the EXE"],
        ["Upgrade path", [r("In-place over v1.2.0 / v1.1.x / v1.0.x — existing ", { size: TABLE_SIZE }), code("config.json"), r(" migrates automatically.", { size: TABLE_SIZE })]],
    ],
));
body.push(para([], { spacing: { after: 200 } }));

body.push(h1("Table of contents"));
body.push(new TableOfContents("Table of Contents", { hyperlink: true, headingStyleRange: "1-3" }));
body.push(new Paragraph({ children: [new PageBreak()] }));

// ===== Headline features =====
body.push(h1("Headline features"));

// 1. Five UI languages
body.push(h2("1. Five UI languages (NL / EN / DE / FR / ES)"));
body.push(para("The application now ships with full translations in five languages, professionally toned for a business audience. A single 226-key StringsTable per language drives every UI element, dialog, status message and audit-tab label."));
body.push(para([
    bold("For the operator: "),
    r("the existing in-app language switcher (Setup → Administrator → User-interface language) becomes a five-entry dropdown showing each language's native name (Nederlands, English, Deutsch, Français, Español). Switching is live; no restart needed."),
]));
body.push(para([
    bold("For first-run users: "),
    r("a small modal language picker now appears when no "),
    code("config.json"),
    r(" exists yet, pre-selected on the Windows UI culture. Picking a language seeds the runtime session; the choice is persisted at the first Setup save."),
]));
body.push(para([
    bold("For SCCM / Intune deployments: "),
    r("an optional MSI command-line property "),
    code("INTERACTIVEMASK_LANGUAGE"),
    r(" skips the first-run picker entirely:"),
]));
body.push(codeBlock("msiexec /i InteractiveMask-1.3.0.msi /qn INTERACTIVEMASK_LANGUAGE=de"));
body.push(para([
    r("The value is written to "),
    code("HKLM\\Software\\InteractiveMask\\InitialLanguage"),
    r(". App.OnStartup reads it on first run only and applies it directly. Without the property, the registry stays untouched and the picker behaves as usual."),
]));

// 2. Long-press
body.push(h2("2. Long-press mass mask / unmask"));
body.push(para([
    r("Hold the left mouse button anywhere on the kiosk window for 500 ms to mask or unmask "),
    bold("every tile at once"),
    r(". The direction follows the current state:"),
]));
body.push(bullet("All tiles already masked → mass-unmask (auth required)."));
body.push(bullet("Anything else (mixed or fully visible) → mass-mask (free; privacy-first)."));
body.push(para([
    r("Mass-unmask is gated by a "),
    bold("state guard"),
    r(": the action only proceeds when every tile is currently masked. A mixed state (some tiles individually privacy-masked by another caregiver) blocks mass-unmask to prevent accidental privacy breach. Authentication for the unmask side follows the existing per-tile policy (PIN or AD); a confirmation dialog before the unmask is opt-in via Setup → Privacy."),
]));
body.push(para([
    italic("Use case: "),
    r("an operator briefly leaves an unattended monitoring station and wants to drop all imagery to privacy in one motion, then restore it on return."),
]));

// 3. Privacy-default
body.push(h2("3. Privacy-default mode"));
body.push(para([
    r("Setup → Privacy gains a "),
    italic("Default visibility"),
    r(" card with two choices:"),
]));
body.push(bullet([
    bold("OversightDefault"),
    r(" (existing v1.2 behaviour). Tiles boot visible. Tap to apply privacy. Removing privacy is auth-aware."),
]));
body.push(bullet([
    bold("PrivacyDefault"),
    r(" (new). Tiles boot blurred. Tap to briefly reveal for verification. An "),
    italic("auto-rollback"),
    r(" timer brings the tile back to blurred. Authentication on each reveal is opt-in via a sub-toggle."),
]));
body.push(para("Privacy-default fits sites with stricter privacy requirements: office spaces, receptions, hotels, schools, and healthcare facilities that prefer blurred-by-default monitoring over the classic visible-by-default model."));
body.push(para("A mode change is treated as a structural change and prompts for a Display restart so the boot state of every tile and the auto-timer direction stay coherent with the chosen mode."));

// 4. Multi-NVR
body.push(h2("4. Multi-NVR support (carried over from v1.2, refined in v1.3)"));
body.push(para([
    r("A single InteractiveMask installation can connect to multiple IDIS NVRs simultaneously and intermix them on one grid. Setup → NVR holds a list editor; Setup → Cameras lets each tile pick its own NVR + camera + stream. One "),
    code("NvrSession"),
    r(" per recorder, with independent connect / disconnect / reconnect state. v1.3 adds:"),
]));
body.push(bullet("A modal NVR-edit dialog (separate from the camera list) for clearer add / edit flows."));
body.push(bullet("Per-NVR connection banner that names the affected recorder when one of several drops."));

// 5. NVR camera-name sync
body.push(h2("5. NVR camera-name sync"));
body.push(para([
    r("Setup → Cameras gains a "),
    italic("Pull names from NVR"),
    r(" button. Click it and the kiosk asks each connected NVR for its "),
    code("G2DEVICE_STATUS._camera_desc"),
    r(" array, then fills a new read-only "),
    bold("NVR Title"),
    r(" column with the camera names as configured on the recorder side. The existing operator-typed "),
    italic("Custom Label"),
    r(" column is "),
    bold("never touched"),
    r(" — operators retain full control over what they want each tile to read in the live grid."),
]));
body.push(para([
    r("Implementation note: the sync routes the request through the "),
    bold("live"),
    r(" Display NVR session via a new "),
    code("NvrSession.FetchCameraNamesAsync(timeout)"),
    r(" method. Most IDIS NVRs reject a second concurrent login from the same user, so a standalone fetcher would have failed. Reusing the existing authenticated session avoids that conflict entirely."),
]));
body.push(para([
    r("NVR Title is now a persisted field on "),
    code("CameraSlotSettings"),
    r(", written to "),
    code("config.json"),
    r(" alongside the operator label, slot, NVR ID, camera index and stream. After a Setup save the kiosk shows the recorder-side name from the first frame, no live device-status round-trip needed."),
]));

// 6. Tile overlay bar
body.push(h2("6. Tile overlay bar redesigned"));
body.push(para("The bottom bar of each tile is now a two-cluster grid:"));
body.push(bullet([
    bold("Left:"),
    r(" status indicator (live / connecting / disconnected) + NVR-side camera title + status text."),
]));
body.push(bullet([
    bold("Right:"),
    r(" operator-typed custom label + privacy badge."),
]));
body.push(para([
    r("Both texts toggle independently from Setup → Privacy → "),
    italic("Tile overlay"),
    r(":"),
]));
body.push(bullet([
    italic("Show custom label"),
    r(" (default on)"),
]));
body.push(bullet([
    italic("Show NVR title"),
    r(" (default off — existing installs don't suddenly grow a second overlay line; turn it on to see the recorder-side name)"),
]));
body.push(para("When both are on, NVR title sits left, custom label sits right."));

// 7. Drag-drop reorder
body.push(h2("7. Drag-and-drop tile reorder"));
body.push(para("Setup → Cameras → Slot bindings grid grows a grip-handle column at the front. Hold the grip and drag a row up or down to change its slot order; slot numbers are renumbered automatically on drop. The whole reorder runs in-memory and only persists when the operator clicks Apply / Save."));

// 8. Apply button
body.push(h2("8. Apply button"));
body.push(para([
    r("Setup gains a third action button between Cancel and Save: "),
    bold("Apply"),
    r(" validates, persists to "),
    code("config.json"),
    r(", and pushes the new settings to the running kiosk via the existing live-apply pipeline — but "),
    bold("leaves the Setup window open"),
    r(". Operators can iterate on changes (camera labels, privacy toggles, etc.) and watch the live grid react without closing and reopening Setup each round."),
]));

// 9. About tab
body.push(h2("9. About tab"));
body.push(para("A new entry in the Setup sidebar shows:"));
body.push(bullet("Product card: runtime version (read via reflection from the assembly), publisher (IDIS Nederland BV), copyright, and a one-paragraph product description."));
body.push(bullet("Integrations: IDIS Direct IP NVR's, SDL2 (zlib license), Authenticode signing (Sectigo Public Code Signing CA R36)."));
body.push(bullet("Open-source components: .NET 9, ASP.NET Core 9, Microsoft.Extensions.*, ProtectedData, WiX Toolset 5, Segoe Fluent / MDL2 Assets — each with license and attribution."));
body.push(bullet([
    r("Bug report card: opens the user's default email client with a pre-filled subject ("),
    code("InteractiveMask vX.Y.Z - bug report"),
    r(") and runtime / OS info in the body, addressed to "),
    code("support@bnl.idisglobal.com"),
    r(". Website link to "),
    code("https://www.idisglobal.solutions"),
    r(" in the Product card."),
]));

// 10. Smaller features
body.push(h2("10. Smaller features"));
body.push(bullet([bold("5×5 grid"), r(" option (25 tiles) added to the existing 1×1 / 2×2 / 3×3 / 4×4 set.")]));
body.push(bullet([bold("IDIS watermark"), r(" (10% opacity) rendered in empty grid tiles so the kiosk feels finished even when slots are unbound.")]));
body.push(bullet([
    bold("Aspect-ratio fix"),
    r(" for non-16:9 cameras. Tile "),
    code("<Image>"),
    r(" switched from "),
    code("Stretch=UniformToFill"),
    r(" (cropped) to "),
    code("Stretch=Uniform"),
    r(" (fit). 4:3, 5:4, 1:1 fish-eye and 21:9 sensors are now shown in full with letterbox / pillarbox bars against the tile background, instead of silently losing edge content."),
]));
body.push(bullet([bold("Setup window"), r(" grew to 1180 × 1000 px so every tab fits without internal scroll on a 1080p display.")]));
body.push(bullet([
    bold("Generalised UI wording"),
    r(': "verzorgers / caregivers / Pflegekräfte / soignants / cuidadores" → "operators / Bediener / opérateurs / operadores" across all five languages, plus broader vertical examples (office, reception, hotel, school, healthcare) in PrivacyDefault help text.'),
]));

// ===== Pre-release engineering pass =====
body.push(h1("Pre-release engineering pass"));
body.push(para("A code-review sweep before tagging surfaced four HIGH-severity issues, all fixed before the build:"));

body.push(buildTable([600, 2700, 2900, 2826],
    ["ID", "Symptom", "Root cause", "Fix"],
    [
        ["H1",
            [r("After a long-press mass-mask, the first individual unmask cleared ", { size: TABLE_SIZE }), code("_activePin"), r("; subsequent reveals went through with no PIN — a security regression.", { size: TABLE_SIZE })],
            [code("ApplyMassMask"), r(" bumped the PIN-service counter once for the whole batch; the per-tile drain ratio was off.", { size: TABLE_SIZE })],
            [r("Bump the counter once per newly-masked tile (", { size: TABLE_SIZE }), code("OnMaskApplied()"), r(" in a loop), plus prompt for a session PIN if the policy requires one and none is active — mirroring ", { size: TABLE_SIZE }), code("ApplyMaskLocal"), r(".", { size: TABLE_SIZE })]],
        ["H2",
            [r("The 500 ms long-press ", { size: TABLE_SIZE }), code("DispatcherTimer"), r(" could orphan and re-fire forever if a mouse-up was missed (modal / UAC / focus loss).", { size: TABLE_SIZE })],
            [r("A new timer was allocated on every mouse-down without stopping a previous one; the old timer's ", { size: TABLE_SIZE }), code("Tick"), r(" handler kept running.", { size: TABLE_SIZE })],
            [r("Single reusable timer field; ", { size: TABLE_SIZE }), code("Stop()"), r(" at the top of every ", { size: TABLE_SIZE }), code("OnRootMouseDown"), r(" and Stop on ", { size: TABLE_SIZE }), code("Tick"), r(" itself. (Also closes L3: per-click allocation.)", { size: TABLE_SIZE })]],
        ["H3",
            [r("Long-running kiosks accumulated dead ", { size: TABLE_SIZE }), code("Strings.Instance.PropertyChanged"), r(" subscriptions over weeks of uptime.", { size: TABLE_SIZE })],
            [code("StreamChoices"), r(" constructor subscribed an inline lambda capturing ", { size: TABLE_SIZE }), code("this"), r("; the subscription was never released.", { size: TABLE_SIZE })],
            [code("StreamChoices"), r(" is now ", { size: TABLE_SIZE }), code("IDisposable"), r(", stores its handler in a field, unsubscribes on ", { size: TABLE_SIZE }), code("Dispose"), r("; ", { size: TABLE_SIZE }), code("SetupWindow.Closed"), r(" disposes it.", { size: TABLE_SIZE })]],
        ["H4",
            [r("A live ", { size: TABLE_SIZE }), code("Privacy.Mode"), r(" flip via Apply silently desynced the running tiles from the new mode (auto-timers anchored against the wrong direction).", { size: TABLE_SIZE })],
            [code("ApplyChangedSettings"), r(" swapped ", { size: TABLE_SIZE }), code("_privacy"), r(" but didn't re-prime tile state for a mode flip; ", { size: TABLE_SIZE }), code("HasStructuralChange"), r(" didn't flag mode changes.", { size: TABLE_SIZE })],
            [code("HasStructuralChange"), r(" now treats any ", { size: TABLE_SIZE }), code("Privacy.Mode"), r(" change as structural so the operator gets the existing restart prompt.", { size: TABLE_SIZE })]],
    ],
));
body.push(para([], { spacing: { after: 200 } }));

body.push(para("Plus three medium fixes:"));
body.push(bullet([
    bold("M1"), r(": dead "), code("_lastCameraNames"), r(" field on "), code("NvrSession"),
    r(" removed (superseded by the "), code("TaskCompletionSource"), r(" mechanism)."),
]));
body.push(bullet([
    bold("M6"), r(": "), italic("Pull names"),
    r(" sync no longer aborts on the first failing NVR — it now collects per-NVR errors, keeps fetching reachable recorders, and surfaces a combined message."),
]));
body.push(bullet([
    bold("M9"), r(": redundant "), code("CameraGrid.CommitEdit"), r(" in the Save flow removed."),
]));
body.push(para("Remaining MEDIUM and LOW findings (dead localized strings, minor allocation in the GDK callback path, etc.) are tracked for a v1.3.x point release."));

// ===== ConfigService persistence =====
body.push(h1("Critical fix during development: ConfigService persistence"));
body.push(para([
    r("Mid-development testing surfaced that "),
    bold("none of the new v1.3 fields actually persisted to disk"),
    r(". The fix:"),
]));
body.push(para([
    code("ConfigService.StoredCamera"),
    r(" and "),
    code("ConfigService.StoredPrivacy"),
    r(" were the v1.2 shape:"),
]));
body.push(bullet([code("Save"), r(" projected only the v1.2 columns into JSON → every v1.3 toggle silently disappeared at write time.")]));
body.push(bullet([code("Load"), r(" read only the v1.2 columns → defaults were re-applied at next start.")]));
body.push(bullet([
    r("The Apply button calls "), code("Load()"), r(" right after "), code("Save()"),
    r('; both paths dropped the new fields, so any v1.3 toggle change "did nothing".'),
]));
body.push(para("Added to the round-trip:"));
body.push(bullet([code("StoredCamera.NvrTitle"), r(" (string)")]));
body.push(bullet([code("StoredPrivacy.ShowMassUnmaskConfirm"), r(" (bool)")]));
body.push(bullet([code("StoredPrivacy.Mode"), r(" (int, mirror of the "), code("PrivacyMode"), r(" enum)")]));
body.push(bullet([code("StoredPrivacy.PrivacyDefaultRequireAuthOnReveal"), r(" (bool)")]));
body.push(bullet([code("StoredPrivacy.ShowCameraLabel"), r(" (bool, default true)")]));
body.push(bullet([code("StoredPrivacy.ShowNvrTitle"), r(" (bool)")]));
body.push(para("Existing v1.2 configs without these properties keep loading: System.Text.Json fills the missing keys with the property defaults (false / 0 / OversightDefault / true for ShowCameraLabel), matching the v1.2 behaviour exactly so nothing observable changes for configs that pre-date this commit."));

// ===== Installer changes =====
body.push(h1("Installer changes"));
body.push(bullet([bold("Self-contained MSI"), r(" (109 MB) bundles .NET 9 + WindowsDesktop + ASP.NET Core. Installs on a clean Windows 10/11 x64 without requiring a separate runtime install — important for SCCM / Intune deployments that don't pre-stage the runtime.")]));
body.push(bullet([
    bold("EV-Authenticode signed"), r(": every "), code("InteractiveMask.*.exe"),
    r(" and "), code("*.dll"),
    r(" plus the MSI itself, with SHA-256 file digest and SHA-256 timestamp digest. SmartScreen recognises the publisher (IDIS Nederland BV) without warnings."),
]));
body.push(bullet([
    r("Optional "), code("INTERACTIVEMASK_LANGUAGE"), bold(" MSI property"),
    r(" for unattended language pre-seed (see Headline features §1)."),
]));

// ===== Breaking changes =====
body.push(h1("Breaking changes"));
body.push(para([bold("None.")]));
body.push(para("All v1.2 configurations load unchanged. Default values for the new fields match v1.2 behaviour exactly:"));

body.push(buildTable([3300, 1900, 3826],
    ["Field", "Default in v1.3", "Behaviour matches"],
    [
        [[code("Privacy.Mode")], [code("OversightDefault")], "v1.2 (always oversight)"],
        [[code("Privacy.ShowCameraLabel")], [code("true")], "v1.2 (label always shown)"],
        [[code("Privacy.ShowNvrTitle")], [code("false")], "v1.2 (NVR title not shown)"],
        [[code("Privacy.ShowMassUnmaskConfirm")], [code("false")], "v1.2 didn't have this; new gesture starts without confirm dialog"],
        [[code("Privacy.PrivacyDefaultRequireAuthOnReveal")], [code("false")], "only relevant in PrivacyDefault mode"],
        [[code("CameraSlotSettings.NvrTitle")], [code('""')], "v1.2 didn't have this; bottom bar simply doesn't show NVR title until first sync"],
    ],
));
body.push(para([], { spacing: { after: 200 } }));

// ===== Upgrade procedure =====
body.push(h1("Upgrade procedure"));
body.push(num([
    r("Install "), code("InteractiveMask-1.3.0.msi"),
    r(". Existing service is stopped; new files replace v1.2; service restarts. No manual config touch required."),
]));
body.push(num([
    r("(Optional) Open Setup → Cameras → "), italic("Pull names from NVR"),
    r(" once, then click Save. Persists the NVR-side camera titles into "),
    code("config.json"),
    r(" so they show up immediately on every restart."),
]));
body.push(num([
    r("(Optional) Setup → Privacy → "), italic("Tile overlay"),
    r(": enable "), italic("Show NVR title"),
    r(" if you want the recorder-side name visible alongside (or instead of) the operator label."),
]));
body.push(num([
    r("(Optional) Setup → Privacy → "), italic("Default visibility"),
    r(": switch to "), italic("Privacy default"),
    r(" if your environment prefers blurred-by-default monitoring. Restart on save."),
]));

// ===== Acknowledgements =====
body.push(h1("Acknowledgements"));
body.push(para("Thanks to the demo audience for the directional feedback that shaped this release: the call for broader-than-zorg framing in the UI text, the practical caregiver-leaving-the-station scenario that drove mass-mask, and the camera-name-from-NVR ask that turned into the new sync flow. The aspect-ratio fix came from a real-world demo with a mixed sensor fleet (4:3 + 1:1 fish-eye + 21:9) where UniformToFill cropping was hiding parts of the cameras' field of view."));

// ----- assemble -------------------------------------------------------------

const doc = new Document({
    creator: "IDIS Nederland BV",
    title: "InteractiveMask v1.3.0 Release Notes",
    description: "Release notes for InteractiveMask v1.3.0",
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
