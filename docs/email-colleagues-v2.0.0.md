# Email to colleagues — InteractiveMask v2.0 available

> Paste the body below into an email. Subject and body are separated so you can copy them independently.

---

**To:** _<colleagues, sales, project management>_
**From:** _<you>_
**Subject:** InteractiveMask 2.0 is ready — what it does and what you can do with it

---

Hi everyone,

**InteractiveMask version 2.0** was finished and signed with our IDIS certificate today. Below is a short summary of what is new and what it means for our customers — no technical terms, just what is in it and why it is useful.

## Quick recap: what does InteractiveMask do?

A Windows application that shows live footage from one or more IDIS NVRs on a screen at the reception desk or in a control room. Operators can switch privacy "on" or "off" on a tile with a single click. Until now that was always a blur applied to the entire camera frame.

## What is new in version 2.0

**The big step: the system now recognises people, cyclists and vehicles by itself.** Only those are made unrecognisable; the rest of the frame stays fully visible. No more blurring the entire frame just because one person walked past the camera.

In practice this means:

1. **Privacy without losing information.** The operator can see what is happening in a space — movement, empty areas, closed doors — while faces and bodies are unrecognisable.
2. **Three categories, each in its own colour.** People are red, bicycles and motorcycles orange, cars and vans blue. Operators can tell at a glance what is being masked.
3. **Configurable per camera.** The camera in the hallway can be set to "people only", the one on the parking lot to "vehicles only". Tailored, not all-or-nothing.
4. **Temporary reveal is possible, gated by PIN or login.** When an authorised reviewer genuinely needs to see who someone is — for instance during an incident — they click the small "AI" pill on the tile, authenticate, pick "30 seconds / 1 minute / 5 minutes / until I remask", and the frame becomes fully visible. After that the AI mask returns automatically. Every reveal is logged: who, which camera, how long.
5. **Automatic throttling when the system gets busy.** If the computer comes under heavy load, AI is suspended on the lowest-priority cameras automatically and restored as soon as load drops. Operators see a small orange "AI paused" badge on the affected tiles. No manual intervention needed.

## What has **not** changed

- The NVR recordings themselves are untouched. Masking is applied on the screen only.
- Existing v1.3 features (mass mask, 5×5 grid, five UI languages, ...) work exactly as before.
- Customers without a graphics card or without an AI use case can simply leave AI off — the app then behaves identically to v1.3.

## What the system **does not** do

Important to know for customer conversations:

- **No facial recognition.** The system detects "a person", not "who".
- **No licence-plate reading.** Detection that a vehicle is present, yes — no OCR.
- **No links to external databases.** Everything runs locally on the host.
- **No plate-string storage.** We do not store any text from plates at all — the concept does not exist in this version.

This is deliberate: GDPR-compliant and legally unambiguous. The system makes visible things less visible; it does not make anything additionally visible.

## Who is this version for?

Customers who already run InteractiveMask and want to upgrade — particularly interesting for:

- **Reception desks and waiting areas** where you want oversight without recognisable faces on the screen
- **Distribution centres and logistics** with cameras on driveways, parking lots, loading docks
- **Education and care facilities** where the privacy of residents / patients / pupils is paramount
- **Retail** with cameras in public-facing areas

## What customers need

- A Windows 10 or 11 PC (64-bit) with a **modern graphics card** (Intel Arc and up, AMD 700-series, or NVIDIA RTX cards). The AI uses the GPU. On a PC without a suitable card the existing (v1.3) features keep working, but the new AI features will not.
- The existing IDIS NVR integration — no change on the NVR side.
- Install once, then it just runs.

## Practical details

- **Installer:** `InteractiveMask-2.0.0.msi` (signed by IDIS Nederland BV)
- **Size:** roughly 180 MB
- **Upgrade from v1.3:** double-click the MSI, the old version is replaced, settings are preserved
- **Unattended rollout** (for IT departments): command-line installation is available, ask us at support@bnl.idisglobal.com

## What we are looking for now

One or two existing customers where we can deploy v2.0 as a pilot, ideally with:

- A site where privacy concerns are already on the table (residents / customers / staff who do not want to be recognisable on a screen)
- An IT contact willing to help with the upgrade
- Willingness to give feedback within 2-4 weeks

If you have a customer in mind: drop me a line and we will schedule a demo or an installation.

## Questions

If anything is unclear, technically or commercially, mail or call me. A short video demo where I show the new features on a live camera is also possible — let me know.

Kind regards,

_<your name>_
_IDIS Nederland BV_

---

> _Attach a file or paste a link: path to `InteractiveMask-2.0.0.msi` on the shared drive or a download link._
