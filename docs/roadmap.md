# InteractiveMask Roadmap

Levend document. Items hier landen pas in een release nadat ze door het product team zijn bevestigd en in een sprint zijn ingepland.

## v1.3.x kandidaten

Onderstaande set is verzameld na de eerste demo-ronde bij prospects en partners (mei 2026). Volgorde is de waarschijnlijke implementatievolgorde, niet de prioriteit.

### 1. Mass-mask en mass-unmask (noodscenario)

**Aanleiding:** veldfeedback. Bij onderbezetting moet een zorgmedewerker met één handeling het hele scherm kunnen maskeren voordat ze van de zorgpost wegloopt, zodat patiëntprivacy bij een onbemande post gewaarborgd is. Andersom moet er ook met één handeling alles tegelijk vrijgegeven kunnen worden.

**Functioneel ontwerp:**
- Twee prominente knoppen in de hoofd-UI, of een keyboard chord (configureerbaar in Setup), of beide.
- **Mask all:** geen authenticatie nodig (privacy-toepassen is altijd vrij, conform huidige policy). Audit log: één enkel event `mass_mask` met de lijst van geraakte tegel-IDs.
- **Unmask all:** authenticatie verplicht (PIN of AD), zelfs als individuele tegels normaal geen auth zouden vragen. Reden: één klik die alle privacy opheft is een hoog-risico actie. Audit log: `mass_unmask` met identiteit van de uitvoerder.
- Optioneel: bevestigingsdialoog vóór de unmask-all, met aftel-timer, te omzeilen met "Niet meer vragen voor deze sessie".

**Open vraag voor stakeholder-overleg:** moet "mass mask" tegelijk de auto-unmask timers van die tegels resetten of negeren?

### 2. Privacy-default mode (omgekeerde standaard) als Setup-optie

Op dit moment is het gedragsmodel: tegels zijn standaard zichtbaar, de zorgmedewerker klikt om te blurren op het moment dat er zorg wordt geleverd (waardigheid tijdens omkleden, wassen, toiletbezoek met assistentie). Auto-unmask zorgt dat het beeld vanzelf weer terugkomt.

Voor sommige settings (bijvoorbeeld kantoorruimtes, gemeenschappelijke ruimtes met permanente camerafeed naar een centrale receptie, of instellingen waar de defaults strikter moeten zijn) is een omgekeerd model wenselijk: tegels standaard geblurd, zorgmedewerker klikt om tijdelijk te onthullen voor verificatie. Daarna automatisch terug naar privé.

**Voorgesteld als configuratie-optie, niet als vervanging:**
- Per-installatie default in Setup: `Mode = OversightDefault | PrivacyDefault`.
- In `PrivacyDefault` mode worden alle tegels bij opstart geblurd; klik onthult, auto-blur timer brengt het terug.
- Audit log onderscheidt tussen "zorgmoment-mask" en "tijdelijke vrijgave-voor-verificatie".
- UI-tekst (banner, knoppen, tooltips) beweegt mee met de gekozen mode.

**Aanleiding:** sales feedback 7 mei 2026, herhaald in demo-feedback. Sterk gewenst voor partnerverhaal naar settings buiten de klassieke zorg-context.

### 3. Drag/drop herordenen van camerategels

Mogelijkheid om de positie van camera's in het grid te wijzigen via drag/drop. Nu zit elke camera vast aan een slot dat in Setup bepaald wordt.

**Open vragen voor stakeholder-overleg:**
- Drag/drop in de Setup, of ook in de live-view? (Live-view is krachtiger, vraagt wel auth voor het opslaan.)
- Wordt de nieuwe volgorde per gebruiker opgeslagen, of globaal voor de installatie?
- Snap-to-grid bij neerleggen, of vrije plaatsing?

**Status:** wachten op extra context van de stakeholder voor functioneel ontwerp.

### 4. Talen toevoegen: Duits, Frans, Spaans

Huidige situatie: Nederlands en Engels.

**Werk:**
- Strings.cs uitbreiden met `de-DE`, `fr-FR`, `es-ES` woordenboeken.
- Professionele vertaling, of als minimum een native-speaker review na een eerste machinevertaling.
- Alle vertalingen tegelijk reviewen om consistentie tussen termen te bewaken (bijvoorbeeld `mask`, `tegel`, `zorgpost`).
- Live language-switcher in Setup uitbreiden.
- Help/handleiding-content (8 hoofdstukken) ook vertalen, of bij eerste oplevering alleen UI en handleiding in NL/EN/DE laten staan met een fallback naar EN voor FR/ES.

### 5. Taalkeuze tijdens installatie en bij eerste opstart

**Probleem:** de app start nu standaard in het Nederlands. Voor niet-Nederlandstalige beheerders is de eerste configuratie daardoor moeilijk te navigeren.

**Voorstel, te combineren:**
- **Installer:** WiX UI dialog krijgt aan het begin een dropdown met talenkeuze. De keuze wordt weggeschreven naar de app-config (`%PROGRAMDATA%\InteractiveMask\config.json` veld `Language`). De vertaalde labels van de installer zelf vragen WiX-localisatiebestanden per taal.
- **Eerste opstart:** als er geen taalkeuze in de config staat, valt de app terug op `CultureInfo.CurrentUICulture` van Windows in plaats van een hardcoded Nederlands. Daarna toont de app eenmalig een welkomstscherm met taalkeuze voordat Setup gestart wordt.

### 6. IDIS-logo (grijs) in lege tegels als visuele mask

Op dit moment zijn lege tegelposities donker. Voorstel: vul ze met een gestyleerd grijs IDIS-logo zodat het grid altijd "af" oogt en de IDIS-merkbeleving consistent is.

**Aangeleverd door:** stakeholder zal het logo aanleveren.

**Voorkeursformaten (in volgorde van wenselijkheid):**
1. **SVG, monochroom (single path of grouped paths)** is het beste, omdat het naadloos schaalt naar elke tegelgrootte (van 1x1 tot 4x4) zonder pixelvervaging. Vul-kleur kan in code overschreven worden zodat we de exacte grijstint kunnen finetunen.
2. **PNG met transparante achtergrond, minimaal 1024 x 1024 px**, voorkeur 2048 x 2048 px. Monochroom of grijswaarden.
3. Als alleen een gekleurde versie beschikbaar is: leveren in de hoogste resolutie die er is, dan converteren wij naar grijs in code.

**Visuele richtlijn:** logo gecentreerd op de tegel, met 30 tot 40 procent opacity tegen de donkere tegelachtergrond, zodat het rustig oogt en niet domineert over de live-tegels eromheen. Stakeholder zal extra context geven over de gewenste plaatsing.

---

## Vragen die nog beantwoord moeten worden voor sprint-planning

- Item 1: drempel voor unmask-all (alleen PIN, of altijd AD wanneer beschikbaar?).
- Item 1: bevestigingsdialoog standaard aan of uit?
- Item 3: drag/drop in Setup, in live-view, of beide?
- Item 4: prioriteit van de drie talen (waarschijnlijk DE eerst gezien IDIS Europa-focus, dan FR, dan ES?).
- Item 6: definitief logo-bestand en gewenste opacity.

---

(Voor latere overweging, niet noodzakelijk v1.3.x: snapshot-export naar PDF, mobiele begeleidende app, integratie met IDIS NVR PTZ-bediening.)
