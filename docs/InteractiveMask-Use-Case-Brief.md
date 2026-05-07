# InteractiveMask — Productbrief voor marketing

**Document:** Use case brief voor marketingafdeling
**Doel:** basis voor publicatiemateriaal, partner handouts, productpagina, sales sheets
**Versie product:** 1.0.6
**Datum:** mei 2026
**Status:** intern werkdocument

---

## Samenvatting

InteractiveMask is een Windows-applicatie die op een centraal monitorscherm in zorginstellingen de live camerabeelden van een IDIS NVR toont. Met één klik op een camerategel kan de zorgmedewerker het beeld onherkenbaar maken zonder de camera fysiek af te dekken of uit te zetten. De NVR blijft gewoon opnemen. Iedere actie wordt vastgelegd in een audit-log met tijdstip, locatie en (optioneel) de Windows-gebruikersnaam van de zorgmedewerker.

De applicatie is ontwikkeld om zorginstellingen te helpen bij het AVG- en WZD-conform invullen van privacy tijdens verzorgingsmomenten, zonder concessies aan de continue veiligheidsbewaking die de camera's bieden.

## Het vraagstuk

Camera's in zorginstellingen dienen meerdere doelen tegelijk: valdetectie, dwaaldetectie, nachtelijk toezicht en personeelsveiligheid. Tegelijk vereisen de Wet Zorg en Dwang (WZD) en de AVG dat de privacy van bewoners bewezen geborgd wordt op momenten dat camerabewaking niet proportioneel is, zoals tijdens wassen, kleden, toiletbezoek of intieme zorghandelingen.

Op dit moment zien we drie patronen op afdelingen:

1. **Camera fysiek afdekken** met een doek of dop. Werkt eenmalig, maar wordt vaak vergeten terug te halen. De camera blijft uren tot dagen onbruikbaar voor veiligheidsbewaking.
2. **Camera in de NVR uitzetten of de stream onderbreken.** De opname stopt en bij een incident is er geen beeld meer. Bij audits van inspectie of zorgverzekeraar is niet aantoonbaar wat er is gebeurd.
3. **Camera laten staan en hopen dat het personeel discreet werkt.** Niet AVG-conform en geen gedocumenteerde maatregel.

Geen van deze drie patronen geeft de zorginstelling controle, traceerbaarheid en gemak tegelijk.

## De oplossing

InteractiveMask plaatst een softwarematig privacy-masker over de individuele camerategel op het kijkscherm. De camera blijft draaien, de NVR blijft opnemen, en alle bestaande veiligheidsfuncties van het IDIS-platform blijven actief. De zorgmedewerker bedient het masker met één muisklik op het 55-inch monitorscherm of vanaf een tablet/telefoon op het kantoor.

Het masker wordt automatisch verwijderd na een instelbare tijdsduur (bijvoorbeeld 5 minuten), met een visuele waarschuwing in de laatste 2 minuten zodat het personeel niet verrast wordt. Handmatig opheffen kan via een sessie-PIN of een Windows Active Directory aanmelding, afhankelijk van het beveiligingsbeleid van de instelling.

## Functionaliteit

**Bediening op het scherm**
- Live grid van 4, 9 of 16 camera's per scherm
- Klik op een tegel zet onmiddellijk een privacy-masker op die ene camera
- Tweede klik vraagt om PIN of Windows-aanmelding om het masker op te heffen
- Tijdslot per masker met visuele aftelling en pulserende waarschuwingsrand
- Statuskleur per tegel: groen (live), oranje (verbinden), rood (verbroken)

**Bediening op afstand**
- Browser-bediening via een ingebouwde webserver
- Werkt op tablet, telefoon of werkstation in hetzelfde netwerk
- Optionele toegangsbeveiliging: open, gedeelde toegangs-PIN of Windows-aanmelding
- Snapshot-thumbnails op verzoek voor visuele bevestiging zonder live videostream

**Beveiliging en logging**
- Iedere mask-actie wordt vastgelegd: tijdstip, camera, bron (lokaal of remote), gebruiker
- Audit-log lokaal opgeslagen plus optioneel realtime doorgegeven aan de SIEM van de instelling via syslog (RFC 5424)
- Wachtwoorden en PIN-codes worden encrypted opgeslagen via Windows DPAPI op machine-niveau
- Kiosk-modus blokkeert toetsenbordcombinaties zoals Alt+Tab en Win-toets zodat gebruikers de applicatie niet onbedoeld verlaten

**Beheer**
- Volledige Nederlandstalige interface, Engels op verzoek, live omschakelbaar
- 8 hoofdstukken interactieve handleiding ingebouwd in de applicatie
- Setup-wizard voor NVR-koppeling, cameratoewijzing, raster-grootte en privacybeleid
- MSI-installer met digitale handtekening (Sectigo EV) voor uitrol via Intune of SCCM

## Doelgroep

**Primair**
- Verpleeghuizen en woonzorgcentra met intramurale zorg
- Gehandicaptenzorg met 24-uurs begeleiding
- Geestelijke gezondheidszorg met afdelingen voor onvrijwillige zorg
- Gespecialiseerde zorgafdelingen met intensieve cameramonitoring (PG, NAH, Korsakov)

**Secundair**
- Beveiligingsbedrijven die zorginstellingen onderhouden
- IDIS-partners die zoeken naar een onderscheidend element in hun aanbod richting zorg
- IT-managers van zorgkoepels die centraal kunnen uitrollen via Intune

## Concrete use cases

**Gebruik 1: Wasronde op de PG-afdeling**
De zorgmedewerker komt om 9 uur de kamer binnen voor de ochtendverzorging. Bij het binnenlopen tikt hij op zijn telefoon één keer op de tegel van die kamer in de browser-app. Het masker activeert direct. Tijdens de verzorging blijft de bewoner zichtbaar voor het personeel in de kamer maar niet voor het camerascherm op kantoor. Na 15 minuten is de verzorging klaar, het masker wordt automatisch opgeheven en de afdeling heeft weer regulier toezicht.

**Gebruik 2: Bezoek met privé karakter**
Een bewoner ontvangt familie met intieme dynamiek (bijvoorbeeld afscheid nemen, een gesprek met geestelijk verzorger). De afdelingsverantwoordelijke activeert het masker via het centrale scherm. De PIN-bescherming voorkomt dat een willekeurige medewerker het masker opheft. Het audit-log toont later wie het masker heeft geactiveerd, voor hoe lang en wie het heeft beëindigd.

**Gebruik 3: Audit door de inspectie**
Bij een audit door IGJ of een interne kwaliteitsmanager kan de instelling het audit-log overleggen. Het toont per dag hoe vaak privacy-maskers actief zijn geweest, op welke camera's en gedurende welke tijdvakken. De NVR-opnames bevestigen dat camera's nooit zijn uitgezet. De zorginstelling kan op deze manier bewijzen dat ze proportioneel met camerabewaking omgaat.

**Gebruik 4: Nachtdienst met beperkte bezetting**
Tijdens de nachtdienst wil het personeel alle ramen volledig open hebben voor toezicht, behalve bij gerichte verzorging. De auto-uit timer staat ingesteld op 10 minuten. Een verzorger die langer bezig is, krijgt een visuele waarschuwing in beeld en kan op afstand verlengen of het masker handmatig opheffen.

## Voordelen voor de zorginstelling

- **Aantoonbare AVG- en WZD-compliance** door een gedocumenteerde, technisch afgedwongen privacy-maatregel met audit-log
- **Geen onderbreking van veiligheidsbewaking**, omdat de camera blijft opnemen op de NVR
- **Bedienbaar door zorgpersoneel zonder IT-training**, één klik volstaat
- **Centraal beheer via standaard Windows-deployment** (Intune, SCCM, GPO)
- **Werkt op bestaande IDIS NVR-infrastructuur**, geen vervanging van camera's of recorder nodig

## Voordelen voor de IDIS-partner

- **Onderscheidende propositie** richting de zorgsector zonder concurrenten op dit specifieke functionele niveau
- **Hogere bindingswaarde** met de eindklant, omdat de oplossing diep in het werkproces van de zorgmedewerker geïntegreerd is
- **Cross-sell met bestaande IDIS-installaties**, geen nieuwe camera-uitrol vereist
- **Marge op software-licentie naast de hardware**

## Compliance en beveiliging

InteractiveMask is gebouwd vanuit een privacy-by-design uitgangspunt:

- Privacy aanzetten gaat zonder authenticatie (privacy gaat altijd voor)
- Privacy uitzetten vereist authenticatie (PIN of Windows-aanmelding)
- Alle mask-acties, PIN-pogingen en NVR-verbindingen worden gelogd
- Optionele realtime forwarding naar het centrale SIEM van de instelling
- Wachtwoorden en certificaten zijn encrypted op machine-niveau
- De webinterface kan los achter aanmelding worden geplaatst, onafhankelijk van het PIN-beleid voor maskers

De volledige applicatie en de installer zijn Authenticode-getekend met SHA-256 en een Sectigo EV-certificaat onder de naam IDIS Nederland BV.

## Deployment

De installatiestap is bewust laag-drempelig gehouden:

- Eén MSI-bestand (108 MB) met alle benodigde runtimes ingebakken
- Werkt op Windows 10 en 11, x64
- Geen .NET of andere prerequisites op de doel-PC nodig
- Registreert de browser-component automatisch als Windows-service
- Voegt de benodigde firewall-regel toe
- IT kan uitrollen via Intune, SCCM of een aangepast GPO-script

## Technische specificaties (samenvatting voor IT)

| Component | Specificatie |
|---|---|
| Doelplatform | Windows 10/11 x64 |
| Runtime | .NET 9 (ingebakken in installer, self-contained) |
| NVR-koppeling | IDIS GDK 6.6.1 via TCP |
| Cameracapaciteit | 16 streams gelijktijdig per kioskscherm |
| Codec ondersteuning | H.264 en H.265 (HEVC) |
| Web-component | ASP.NET Core, HTTP en HTTPS, lokaal of LAN-bind |
| Authenticatie | sessie-PIN (4 cijfers) of Windows AD/LDAP |
| Audit-formaat | NDJSON lokaal, optioneel RFC 5424 syslog |
| Codetekening | Sectigo Public Code Signing CA R36 (EV) |

## Vervolg voor marketing

Suggesties voor materiaal dat dit document kan voeden:

- **Productpagina** met de drie kerngebruiksscenario's en bijbehorende screenshots
- **One-pager voor partner handouts** (A4, voor- en achterzijde)
- **Korte demo-video** van een mask-actie en het audit-overzicht in setup
- **Testimonial-template** voor een eerste pilot-klant in de zorg
- **Whitepaper privacy-by-design in zorgcamera-toezicht** voor IGJ-georiënteerde lezers
- **Webinar of partner-bijeenkomst** rond AVG/WZD-conforme camerabewaking

Beeldmateriaal en logo's zijn beschikbaar in de repository onder `images/` (Windows store sizes, favicons, app-iconen). De codebase is intern beheerd via GitHub onder de IDIS-organisatie.

---

*One Solution. One Company.*
