# InteractiveMask Roadmap

Levend document. Items hier landen pas in een release nadat ze door het product team zijn bevestigd en in een sprint zijn ingepland.

## v1.3.x kandidaten

Onderstaande set is verzameld na de eerste demo-ronde bij prospects en partners (mei 2026). Volgorde is de waarschijnlijke implementatievolgorde, niet de prioriteit.

### 1. Mass-mask en mass-unmask (noodscenario)

**Aanleiding:** veldfeedback. Bij onderbezetting moet een zorgmedewerker met één handeling het hele scherm kunnen maskeren voordat ze van de zorgpost wegloopt, zodat patiëntprivacy bij een onbemande post gewaarborgd is. Andersom moet er ook met één handeling alles tegelijk vrijgegeven kunnen worden, mits dat veilig kan.

**Functioneel ontwerp:**
- Twee prominente knoppen in de hoofd-UI, of een keyboard chord (configureerbaar in Setup), of beide.
- **Mask all:** altijd toegestaan, geen authenticatie nodig (privacy-toepassen is altijd vrij, conform huidige policy). Audit log: één enkel event `mass_mask` met de lijst van geraakte tegel-IDs.
- **Unmask all:** authenticatie verplicht (PIN of AD), zelfs als individuele tegels normaal geen auth zouden vragen. Reden: één klik die alle privacy opheft is een hoog-risico actie.
- **Belangrijke veiligheidsregel (stakeholder-besluit 8 mei 2026):** mass-unmask werkt alleen als de previous state óók volledig vrij was, dat wil zeggen: alleen wanneer mass-unmask het inverse is van een voorafgaande mass-mask op alle tegels tegelijk. Zodra er individuele masks staan (gemengde state) wordt mass-unmask geweigerd met een nette melding ("Er staan handmatige privacy-masks actief. Hef die individueel op om risico op accidentele privacy-doorbraak te voorkomen."). Dit voorkomt dat een zorgmedewerker per ongeluk een privacy-mask opheft die een collega bewust gezet heeft voor een lopend zorgmoment.
- Audit log: `mass_unmask` event met identiteit en het aantal geraakte tegels. Geweigerde pogingen worden gelogd als `mass_unmask_blocked` met de reden.

**Configuratie in Setup:**
- Bevestigingsdialoog vóór de unmask-all is **standaard uit**, in te schakelen via Setup. Stakeholder houdt zich het recht voor om de default later om te draaien op basis van veldfeedback.

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

### 3. Drag/drop herordenen in Setup, slot bindings

Beheerder kan rijen in de tabel `Cameras > Slot bindings` op- of neerschuiven via drag/drop. Het slotnummer (1-N) volgt de positie in de tabel, dus drag/drop wijzigt direct welke camera op welk grid-slot terechtkomt. Opslaan via dezelfde Save-knop als de rest van Setup.

**Scope (stakeholder-besluit 8 mei 2026):**
- Alleen in Setup, alleen voor de beheerder.
- **Niet** in de live-view en **niet** door eindgebruikers, althans niet in v1.3.x. Stakeholder laat zich het recht voor om in een latere release een Setup-toggle "eindgebruikers mogen tegels verplaatsen" toe te voegen.
- Volgorde wordt globaal opgeslagen voor de installatie (één slot-mapping per machine, zoals nu).

**Implementatie-notes:**
- WPF DataGrid ondersteunt geen drag/drop out of the box; we gebruiken een vergelijkbare oplossing als bij andere gelijksoortige componenten (eigen drag-adorner, drop-target highlight, slot-nummers automatisch herberekenen).
- Visuele feedback: rij wordt iets uitgelicht tijdens het slepen, een lijn toont de drop-positie tussen rijen.

### 4. Talen toevoegen: Duits, Frans, Spaans

Huidige situatie: Nederlands en Engels. Marktanalyse stakeholder 8 mei 2026:

| Markt | Talen | Prioriteit |
|---|---|---|
| Europa (kernmarkt) | NL, DE, FR, EN | NL en EN aanwezig, **DE en FR toevoegen** |
| Verenigde Staten | EN, ES | EN aanwezig, **ES toevoegen** |

Implementatievolgorde voorgesteld: **DE eerst** (grootste kwantiteit prospects in pipeline), **FR daarna**, **ES als derde**. Alle drie wel binnen v1.3.x leveren zodat we de releasecycli niet versnipperen.

**Werk:**
- `Strings.cs` uitbreiden met `de-DE`, `fr-FR`, `es-ES` woordenboeken naast de bestaande `nl-NL` en `en-US`.
- Professionele vertaling, of als minimum een native-speaker review na een eerste machinevertaling. Glossarium voorbereiden voor consistentie tussen termen (`mask`, `tegel`, `zorgpost`, `bewoner`, `tegel`).
- Live language-switcher in Setup uitbreiden, alle vijf talen.
- Help/handleiding-content (8 hoofdstukken) idealiter ook vertalen. Bij capaciteitsdruk: minimaal NL/EN/DE compleet, FR/ES initieel via fallback naar EN voor de help-tekst, terwijl de UI wel volledig vertaald is.

### 5. Taalkeuze tijdens installatie en bij eerste opstart

**Probleem:** de app start nu standaard in het Nederlands. Voor niet-Nederlandstalige beheerders is de eerste configuratie daardoor moeilijk te navigeren.

**Voorstel, te combineren:**
- **Installer:** WiX UI dialog krijgt aan het begin een dropdown met talenkeuze. De keuze wordt weggeschreven naar de app-config (`%PROGRAMDATA%\InteractiveMask\config.json` veld `Language`). De vertaalde labels van de installer zelf vragen WiX-localisatiebestanden per taal.
- **Eerste opstart:** als er geen taalkeuze in de config staat, valt de app terug op `CultureInfo.CurrentUICulture` van Windows in plaats van een hardcoded Nederlands. Daarna toont de app eenmalig een welkomstscherm met taalkeuze voordat Setup gestart wordt.

### 6. IDIS-logo in lege tegels als visuele mask

Op dit moment zijn lege tegelposities donker. Voorstel: vul ze met een subtiel IDIS-logo zodat het grid altijd "af" oogt en de IDIS-merkbeleving consistent is.

**Stakeholder-besluit 8 mei 2026:**
- Formaat: **PNG**, aangeleverd door stakeholder.
- Opacity: **10 procent**. Heel licht zichtbaar, niet dominant.
- Plaatsing: gecentreerd op de tegel, schaalt mee met de tegelgrootte.

**Acceptatiecriteria voor het PNG-bestand:**
- Transparante achtergrond.
- Minimaal 1024 x 1024 px, voorkeur 2048 x 2048 px, zodat ook in een 1x1 grid (volledig scherm één tegel) het beeld scherp blijft.
- Monochroom of grijswaarden, of een volledig gekleurde versie waar wij in code de uiteindelijke kleur en de 10 procent opacity-laag op leggen.

**Status:** logo aangeleverd. Bron `IDIS_White_Logo.webp` (400 x 200, transparant, wit). Geconverteerd naar PNG en opgenomen in de repo als `images/idis-logo-mask.png` (32-bit ARGB met alpha-kanaal).

**Aandachtspunt resolutie:** 400 x 200 is wat krap voor een 1x1 fullscreen-grid op 4K. Bij 10 procent opacity valt eventuele schaalvaagheid waarschijnlijk weg, maar als er een hogere resolutie beschikbaar is (voorkeur 2048 x 1024 of vector-bron) kunnen we het bestand later vervangen zonder code-wijziging.

---

## Open punten voor sprint-planning

- Item 1: definitieve drempel voor unmask-all-auth: alleen PIN, of altijd AD wanneer AD-mode actief is. **Voorstel:** volg de bestaande mode (AD-mode = AD-credentials vereist, PIN-mode = PIN). Te bevestigen.
- Item 1: moet "mass mask" tegelijk de auto-unmask timers van die tegels resetten of negeren?
- Item 6: aanlevering definitief logo-bestand (PNG, transparant, hoge resolutie). Stakeholder werkt hieraan.

---

(Voor latere overweging, niet noodzakelijk v1.3.x: snapshot-export naar PDF, mobiele begeleidende app, integratie met IDIS NVR PTZ-bediening.)
