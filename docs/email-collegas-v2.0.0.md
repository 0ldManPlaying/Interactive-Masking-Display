# E-mail aan collega's — InteractiveMask v2.0 beschikbaar

> Plak de tekst hieronder in een mail. Onderwerp + body zijn los te kopiëren.

---

**Aan:** _<collega's, sales, projectmanagement>_
**Van:** _<jij>_
**Onderwerp:** InteractiveMask 2.0 is klaar — wat het kan en wat jullie ermee kunnen

---

Hallo allemaal,

Vandaag is **InteractiveMask versie 2.0** afgerond en ondertekend met ons IDIS-certificaat. Hieronder in het kort wat er nieuw is en wat het voor onze klanten betekent — geen techniek, gewoon wat erin zit en waarom dat handig is.

## Wat doet InteractiveMask ook alweer

Een Windows-applicatie die op een scherm bij de balie of in een controlekamer live beeld toont van één of meer IDIS NVR's. Operators kunnen met één klik de privacy "aan" of "uit" zetten op een tegel. Tot nu toe was dat altijd een vlak van wazigheid over het hele cameraveld.

## Wat is er nieuw in versie 2.0

**De grote stap: het systeem herkent zélf personen, fietsers en voertuigen in beeld.** Alleen die worden onherkenbaar gemaakt, de rest blijft gewoon zichtbaar. Geen wazig vlak meer over het hele beeld als er maar één persoon door de camera loopt.

In de praktijk betekent dat:

1. **Privacy zonder informatieverlies.** De operator ziet wat er in een ruimte gebeurt — beweging, lege ruimtes, gesloten deuren — terwijl gezichten en lichamen onherkenbaar zijn.
2. **Drie categorieën, elk in een eigen kleur.** Personen worden rood, fietsen en motors oranje, auto's en bestelbussen blauw. Operators zien in één oogopslag wát er geblurd is.
3. **Per camera in te stellen.** De camera in de hal kan op "alleen personen" staan, de camera bij de parkeerplaats op "alleen voertuigen". Klantgericht, geen "alles of niets".
4. **Tijdelijk weghalen kan, met PIN of inlog.** Als een geautoriseerde reviewer écht moet weten wie iemand is — bijvoorbeeld een incident — dan klikt hij op een klein "AI"-pilletje op de tegel, logt in, kiest "30 seconden / 1 minuut / 5 minuten / tot ik op her-mask druk" en het beeld wordt heel even compleet zichtbaar. Daarna gaat het automatisch weer aan. Elke onthulling wordt gelogd: wie, welke camera, hoe lang, om welke reden.
5. **Automatisch terugschakelen als het systeem druk wordt.** Als de computer onder zware belasting komt, schakelt de AI vanzelf op laagste-prioriteit-cameras uit en weer aan zodra het rustiger is. Operators zien dan een klein oranje labeltje "AI gepauzeerd" op die tegel. Geen handmatige tussenkomst nodig.

## Wat is **niet** veranderd

- De NVR-opnames blijven onaangetast. Het maskeren gebeurt alleen op het scherm.
- De bestaande v1.3-functies (mass mask, 5×5 grid, vijf talen, ...) werken precies hetzelfde.
- Klanten zonder grafische kaart of zonder behoefte aan AI kunnen de AI gewoon uitlaten — dan gedraagt de app zich identiek aan v1.3.

## Wat doet het systeem **niet**

Belangrijk om te weten voor klantgesprekken:

- **Geen gezichtsherkenning.** Het systeem detecteert "een persoon", niet "wie".
- **Geen kenteken-lezen.** Wel detectie dat er een voertuig staat, geen OCR.
- **Geen koppeling met externe databases.** Alles draait lokaal op de host.
- **Geen plate-string-opslag.** We slaan helemaal geen tekst van kentekens op — het concept bestaat niet in deze versie.

Dit is bewust zo gekozen: GDPR-conform en juridisch helder. Het systeem maakt zichtbaar minder zichtbaar, het maakt niets méér zichtbaar.

## Voor wie is deze versie geschikt

Klanten die nu al InteractiveMask hebben en willen upgraden — vooral interessant voor:

- **Receptiebalies en wachtruimtes** waar je wél overzicht wil maar geen herkenbare gezichten op het scherm
- **Distributiecentra en logistiek** met camera's op de oprit, parkeerterrein, laaddok
- **Onderwijs en zorglocaties** waar privacy van bewoners / patiënten / leerlingen voorop staat
- **Retail** met camera's in publieke ruimtes

## Wat klanten nodig hebben

- Een Windows 10 of 11 PC (64-bit) met een **moderne grafische kaart** (vanaf Intel Arc, AMD 700-serie of NVIDIA RTX-kaart). De AI gebruikt de GPU. Op een PC zonder geschikte kaart blijven de oude (v1.3) functies werken, maar de nieuwe AI-features niet.
- De bestaande IDIS NVR-koppeling — geen verandering aan de NVR-kant.
- Eén keer installeren, daarna werkt het.

## Praktisch

- **Installer:** `InteractiveMask-2.0.0.msi` (ondertekend door IDIS Nederland BV)
- **Grootte:** circa 180 MB
- **Upgrade vanaf v1.3:** dubbelklik op de MSI, oude versie wordt vervangen, instellingen blijven behouden
- **Onbeheerde uitrol** (voor IT-afdelingen): commandoregel-installatie beschikbaar, vraag dit gerust aan ons via support@bnl.idisglobal.com

## Wat we voor de pilot nu zoeken

Eén of twee bestaande klanten waar we de v2.0 als pilot kunnen plaatsen, idealiter met:

- Een locatie waar privacy-zorgen al spelen (bewoners / klanten / personeel willen niet herkenbaar op een scherm)
- IT die meedenkt over de upgrade
- Bereidheid om binnen 2-4 weken feedback te geven

Als je een klant in gedachten hebt: stuur mij een seintje, dan plannen we een demo of een installatie in.

## Vragen

Als er onduidelijkheden zijn, technisch of commercieel, mail of bel me gerust. Een korte video-demo waarin ik de nieuwe features laat zien op een live camera kan ook — laat het me weten.

Vriendelijke groet,

_<jouw naam>_
_IDIS Nederland BV_

---

> _Bijlage / link toevoegen: pad naar `InteractiveMask-2.0.0.msi` op de gedeelde schijf of een download-link._
