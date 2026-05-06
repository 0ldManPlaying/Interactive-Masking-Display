namespace InteractiveMask.Display;

/// <summary>
/// All user-visible strings for the in-app help window. Mirrors the
/// <see cref="StringsTable"/> pattern: two static immutable tables (Nl, En)
/// resolved at runtime via <see cref="Strings.Instance"/>. This keeps the help
/// content in sync with whatever language the rest of the UI is showing.
/// Care has been taken to keep wording accessible to caregivers — internal
/// concepts like IPC, WebHost or DPAPI are either avoided or only mentioned
/// briefly in the administrator chapter.
/// </summary>
public sealed class HelpStrings
{
    // Window chrome
    public string WindowTitle { get; init; } = "";
    public string PageTitle { get; init; } = "";
    public string PageSubtitle { get; init; } = "";
    public string ButtonClose { get; init; } = "";

    // Sidebar navigation labels
    public string NavWelcome { get; init; } = "";
    public string NavScreen { get; init; } = "";
    public string NavPrivacyOn { get; init; } = "";
    public string NavPrivacyOff { get; init; } = "";
    public string NavAutoOff { get; init; } = "";
    public string NavRemote { get; init; } = "";
    public string NavAdmin { get; init; } = "";
    public string NavApi { get; init; } = "";
    public string NavFaq { get; init; } = "";

    // 1. Welcome
    public string WelcomeTitle { get; init; } = "";
    public string WelcomeIntro { get; init; } = "";
    public string WelcomeWhoFor { get; init; } = "";
    public string WelcomeWhoForBody { get; init; } = "";
    public string WelcomeWhatIsMaskTitle { get; init; } = "";
    public string WelcomeWhatIsMaskBody { get; init; } = "";

    // 2. Screen
    public string ScreenTitle { get; init; } = "";
    public string ScreenIntro { get; init; } = "";
    public string ScreenStatusTitle { get; init; } = "";
    public string ScreenStatusGreen { get; init; } = "";
    public string ScreenStatusAmber { get; init; } = "";
    public string ScreenStatusRed { get; init; } = "";
    public string ScreenEmptyTile { get; init; } = "";
    public string ScreenLegendCaption { get; init; } = "";
    public string ScreenTileLive { get; init; } = "";
    public string ScreenTileConnecting { get; init; } = "";
    public string ScreenTileLost { get; init; } = "";
    public string ScreenTileMasked { get; init; } = "";

    // 3. Privacy on
    public string PrivacyOnTitle { get; init; } = "";
    public string PrivacyOnIntro { get; init; } = "";
    public string PrivacyOnSteps { get; init; } = "";
    public string PrivacyOnFirstPin { get; init; } = "";
    public string PrivacyOnTip { get; init; } = "";

    // 4. Privacy off
    public string PrivacyOffTitle { get; init; } = "";
    public string PrivacyOffIntro { get; init; } = "";
    public string PrivacyOffPinFlow { get; init; } = "";
    public string PrivacyOffAdFlow { get; init; } = "";
    public string PrivacyOffLockoutTitle { get; init; } = "";
    public string PrivacyOffLockoutBody { get; init; } = "";

    // 5. Auto-off timer
    public string AutoOffTitle { get; init; } = "";
    public string AutoOffIntro { get; init; } = "";
    public string AutoOffWarningRing { get; init; } = "";
    public string AutoOffNote { get; init; } = "";
    public string AutoOffPhaseStart { get; init; } = "";
    public string AutoOffPhaseWarning { get; init; } = "";
    public string AutoOffPhaseDone { get; init; } = "";

    // 6. Remote control
    public string RemoteTitle { get; init; } = "";
    public string RemoteIntro { get; init; } = "";
    public string RemoteUrlNote { get; init; } = "";
    public string RemoteSamePin { get; init; } = "";
    public string RemoteShared { get; init; } = "";

    // 7. Admin
    public string AdminTitle { get; init; } = "";
    public string AdminOpening { get; init; } = "";
    public string AdminTabs { get; init; } = "";
    public string AdminKiosk { get; init; } = "";
    public string AdminAd { get; init; } = "";
    public string AdminAudit { get; init; } = "";
    public string AdminTechNote { get; init; } = "";

    // 8. API & Integration
    public string ApiTitle { get; init; } = "";
    public string ApiIntro { get; init; } = "";
    public string ApiBaseTitle { get; init; } = "";
    public string ApiBaseBody { get; init; } = "";
    public string ApiAuthTitle { get; init; } = "";
    public string ApiAuthBody { get; init; } = "";
    public string ApiEndpointsTitle { get; init; } = "";

    public string ApiStateMethod { get; init; } = "";
    public string ApiStateBody { get; init; } = "";
    public string ApiAuthModeMethod { get; init; } = "";
    public string ApiAuthModeBody { get; init; } = "";
    public string ApiToggleMethod { get; init; } = "";
    public string ApiToggleBody { get; init; } = "";
    public string ApiSnapshotMethod { get; init; } = "";
    public string ApiSnapshotBody { get; init; } = "";
    public string ApiAuditMethod { get; init; } = "";
    public string ApiAuditBody { get; init; } = "";

    public string ApiAccessTitle { get; init; } = "";
    public string ApiAccessBody { get; init; } = "";
    public string ApiAccessModeMethod { get; init; } = "";
    public string ApiAccessModeBody { get; init; } = "";
    public string ApiLoginMethod { get; init; } = "";
    public string ApiLoginBody { get; init; } = "";
    public string ApiLogoutMethod { get; init; } = "";
    public string ApiLogoutBody { get; init; } = "";

    public string ApiIpcTitle { get; init; } = "";
    public string ApiIpcBody { get; init; } = "";

    // 9. FAQ
    public string FaqTitle { get; init; } = "";
    public string FaqQ1 { get; init; } = "";
    public string FaqA1 { get; init; } = "";
    public string FaqQ2 { get; init; } = "";
    public string FaqA2 { get; init; } = "";
    public string FaqQ3 { get; init; } = "";
    public string FaqA3 { get; init; } = "";
    public string FaqQ4 { get; init; } = "";
    public string FaqA4 { get; init; } = "";
    public string FaqQ5 { get; init; } = "";
    public string FaqA5 { get; init; } = "";
    public string FaqQ6 { get; init; } = "";
    public string FaqA6 { get; init; } = "";

    public static HelpStrings ForCurrentLanguage() =>
        string.Equals(Strings.Instance.LanguageCode, "en", System.StringComparison.OrdinalIgnoreCase)
            ? En
            : Nl;

    public static readonly HelpStrings Nl = new()
    {
        WindowTitle  = "InteractiveMask — Handleiding",
        PageTitle    = "Handleiding",
        PageSubtitle = "Hoe u het camerascherm en de privacyknoppen gebruikt.",
        ButtonClose  = "Sluiten",

        NavWelcome    = "Welkom",
        NavScreen     = "Het scherm",
        NavPrivacyOn  = "Privacy aan zetten",
        NavPrivacyOff = "Privacy uit zetten",
        NavAutoOff    = "Auto-uit timer",
        NavRemote     = "Bediening op afstand",
        NavAdmin      = "Voor beheerders",
        NavApi        = "API en integratie",
        NavFaq        = "Veelgestelde vragen",

        WelcomeTitle = "Welkom",
        WelcomeIntro = "Met deze applicatie ziet u live de camerabeelden van de afdeling op één overzicht. " +
                       "Met één klik op een tegel kunt u dat beeld onherkenbaar maken — dat heet een privacy-masker.",
        WelcomeWhoFor = "Voor wie is dit?",
        WelcomeWhoForBody = "Voor verzorgers en begeleiders. U hoeft geen technische kennis te hebben. " +
                            "Voor het instellen van camera’s en het netwerk is een aparte beheerderspagina aanwezig (zie “Voor beheerders”).",
        WelcomeWhatIsMaskTitle = "Wat is een privacy-masker?",
        WelcomeWhatIsMaskBody = "Een privacy-masker legt een waas over één camerategel zodat u het tegel niet meer in detail kunt zien, terwijl de camera blijft opnemen op de NVR-recorder. " +
                                "Zo blijft de privacy van bewoners gewaarborgd op momenten dat dat nodig is.",

        ScreenTitle = "Het scherm",
        ScreenIntro = "Het hoofdscherm bestaat uit een raster van camerategels. Iedere tegel toont één camera. " +
                      "Het bolletje linksonder vertelt u hoe het ervoor staat met die camera.",
        ScreenStatusTitle = "Statuskleuren",
        ScreenStatusGreen = "Groen — live beeld",
        ScreenStatusAmber = "Oranje — verbinding wordt opgebouwd",
        ScreenStatusRed   = "Rood — geen beeld of verbinding verbroken",
        ScreenEmptyTile   = "Een tegel met de tekst “leeg” betekent dat er nog geen camera aan deze positie is gekoppeld.",
        ScreenLegendCaption = "Voorbeeldraster met de vier toestanden waarin een tegel kan staan:",
        ScreenTileLive       = "Live",
        ScreenTileConnecting = "Verbinden",
        ScreenTileLost       = "Verbroken",
        ScreenTileMasked     = "Masker actief",

        PrivacyOnTitle = "Privacy aan zetten",
        PrivacyOnIntro = "Wanneer u privacy aan zet wordt het beeld op die tegel direct waziger. Dit gebeurt op het scherm zelf; de NVR blijft gewoon opnemen.",
        PrivacyOnSteps = "Klik (of tik) één keer op de tegel die u onherkenbaar wilt maken. " +
                         "Het waas verschijnt onmiddellijk en de tegel toont “Privacy actief”.",
        PrivacyOnFirstPin = "Bij de eerste keer dat u in een sessie een masker plaatst, kan het systeem vragen om een 4-cijferige sessie-PIN te kiezen. " +
                            "Diezelfde PIN gebruikt u later om de maskers weer uit te zetten.",
        PrivacyOnTip = "U kunt zoveel tegels tegelijk maskeren als u wilt. Iedere klik werkt onafhankelijk van de andere.",

        PrivacyOffTitle = "Privacy uit zetten",
        PrivacyOffIntro = "Klik nogmaals op een tegel met privacy-masker om het te verwijderen. Afhankelijk van de instellingen op uw locatie wordt u dan om een PIN of een Windows-aanmelding gevraagd.",
        PrivacyOffPinFlow = "Met sessie-PIN: er verschijnt een toetsenblok. Voer de 4 cijfers in die u bij de eerste mask hebt gekozen.",
        PrivacyOffAdFlow = "Met Windows-aanmelding: voer uw gebruikersnaam en wachtwoord in. Uw naam komt in het audit-log te staan.",
        PrivacyOffLockoutTitle = "Wat als ik de PIN te vaak fout typ?",
        PrivacyOffLockoutBody = "Na drie verkeerde pogingen blokkeert het toetsenblok 30 seconden. " +
                                "Wacht totdat de timer op nul staat en probeer het rustig opnieuw.",

        AutoOffTitle = "Auto-uit timer",
        AutoOffIntro = "Een privacy-masker kan automatisch verlopen na een aantal minuten. Zo is er geen risico dat een masker per ongeluk de hele dag aan blijft staan.",
        AutoOffWarningRing = "Vlak voordat het masker automatisch wegvalt, krijgt de tegel een pulserende oranje rand en de tekst “auto-uit” in beeld. Dat is uw waarschuwing.",
        AutoOffNote = "Standaard is bijvoorbeeld auto-uit = 5 minuten en waarschuwing = 2 minuten. De beheerder kan dit aanpassen op de Privacy-tab.",
        AutoOffPhaseStart   = "0 min – masker geplaatst",
        AutoOffPhaseWarning = "3 min – oranje rand",
        AutoOffPhaseDone    = "5 min – masker weg",

        RemoteTitle = "Bediening op afstand",
        RemoteIntro = "U kunt dezelfde tegels ook in een gewone webbrowser bedienen — op een tablet, een ander werkstation, of een telefoon op het kantoor.",
        RemoteUrlNote = "Open in de browser: http://<naam-van-de-kioskcomputer>:8080. Uw beheerder vertelt u welk adres dat is op uw locatie.",
        RemoteSamePin = "Klik in de browser werkt precies hetzelfde als op het scherm zelf. Bij het uitzetten van een masker wordt ook in de browser om uw PIN of aanmelding gevraagd.",
        RemoteShared = "U kunt tegelijk lokaal én op afstand werken. De sessie-PIN is hetzelfde voor beide kanten.",

        AdminTitle = "Voor beheerders",
        AdminOpening = "Het beheerdersgedeelte opent u via rechter-muisklik op het hoofdscherm → Setup… → admin-PIN intypen. " +
                       "Of klik op deze knop in dit help-venster als u al in setup bent.",
        AdminTabs = "Tabs in setup: NVR (recorder-verbinding), Cameras (welke camera op welke positie), Grid (raster-grootte), Privacy (PIN-beleid en auto-uit), Web-UI (poorten en certificaat), Audit-log, en Beheerder (admin-PIN, taal, kiosk-modus).",
        AdminKiosk = "Kiosk-modus blokkeert toetsen zoals Alt+Tab, Win-toets en Alt+F4 zodat de gebruikers niet per ongeluk uit de applicatie raken. De beheerder kan altijd terug via rechter-muisklik en admin-PIN.",
        AdminAd = "Identiteit verzorgers (AD-modus) wisselt het sessie-PIN-toetsenblok om naar een Windows-aanmelding. Zo legt het audit-log vast wie elk masker heeft uitgezet.",
        AdminAudit = "Op de Audit-tab ziet u alle mask-acties, PIN-pogingen en NVR-verbindingen met tijdstip, bron en eventuele detail. Met “Export naar CSV…” kunt u een logboek bewaren of doorsturen.",
        AdminTechNote = "Technische opmerking voor de beheerder: het wachtwoord van de NVR en het certificaat worden versleuteld bewaard via Windows DPAPI op machine-niveau. " +
                        "De browser-bediening loopt via een ingebouwde webserver (WebHost) die u op de Web-UI-tab op het LAN kunt openzetten of beperken tot localhost.",

        ApiTitle = "API en integratie",
        ApiIntro = "De WebHost-component biedt een kleine, lokale REST-API zodat een browser, " +
                   "tablet of een ander beheersysteem dezelfde acties kan uitvoeren als de kiosk zelf. " +
                   "Alle endpoints accepteren en retourneren JSON, behalve het snapshot-endpoint dat een JPEG-binary teruggeeft.",
        ApiBaseTitle = "Basis-URL",
        ApiBaseBody = "http://<naam-van-de-kioskcomputer>:<HttpPort> — standaard poort 8080. " +
                      "Indien geconfigureerd is HTTPS beschikbaar op de aparte poort uit Setup → Web-UI. " +
                      "De API luistert standaard alleen op localhost; vink in Setup “Toegankelijk op het LAN” aan om vanaf andere apparaten te bereiken.",
        ApiAuthTitle = "Authenticatie-modus",
        ApiAuthBody = "De server bepaalt zelf de authenticatie-flow. /api/auth-mode geeft “pin”, “ad” of “off” terug. " +
                      "In “pin”-mode stuurt de client de sessie-PIN bij /api/toggle. In “ad”-mode stuurt de client gebruikersnaam + wachtwoord; " +
                      "de WebHost valideert lokaal via Windows LogonUser en geeft alleen het pre-geauthentiseerde toggle-commando door aan de Display-kant. " +
                      "Wachtwoorden gaan dus nooit over de IPC-verbinding.",
        ApiEndpointsTitle = "Endpoints",

        ApiStateMethod = "GET /api/state",
        ApiStateBody = "Volledige snapshot van het raster en de tegelstatus (slot, label, masker-aan/uit, status, countdown). " +
                       "Gebruikt door de browser-UI; pollt standaard 1× per seconde.",
        ApiAuthModeMethod = "GET /api/auth-mode",
        ApiAuthModeBody = "Geeft { mode, domain } terug. mode = \"pin\" | \"ad\" | \"off\". domain is alleen gevuld in AD-mode.",
        ApiToggleMethod = "POST /api/toggle",
        ApiToggleBody = "Body: { slot, pin?, username?, password? }. Antwoord-result: Ok / PinRequired / PinWrong / LockedOut / CredentialsRequired / CredentialsWrong / InvalidSlot. " +
                        "In PIN-mode levert een eerste klik PinRequired; herhaal met de PIN. In AD-mode levert een unmask CredentialsRequired; herhaal met username + password.",
        ApiSnapshotMethod = "GET /api/snapshot/{slot}",
        ApiSnapshotBody = "Eenmalige JPEG-snapshot van de huidige frame (image/jpeg). " +
                          "200 = OK met JPEG-bytes, 403 = privacy-masker actief (geweigerd), 404 = geen camera of geen frame. " +
                          "Bedoeld voor handmatige refresh in browser/tablet — geen periodieke polling.",
        ApiAuditMethod = "GET /api/audit?limit=N",
        ApiAuditBody = "Tail van het audit-log (NDJSON-events als JSON-array). limit standaard 100, max 5000.",

        ApiAccessTitle = "Toegangsbeveiliging van de web-interface",
        ApiAccessBody = "Naast het PIN/AD-beleid voor het uitzetten van een masker kan de web-interface zelf ook achter aanmelding worden geplaatst. Modus instellen via Setup → Web-UI → Toegang tot de web-interface. Drie modi: \"off\" (open, standaard), \"pin\" (gedeelde toegangs-PIN, DPAPI-encrypted opgeslagen), \"ad\" (Windows-aanmelding). Bij modus \"pin\" of \"ad\" stuurt de server bij elke onbeveiligde route een 401 (API) of redirect naar /login (pagina). Sessies leven 8 uur met sliding window via een HttpOnly-cookie.",
        ApiAccessModeMethod = "GET /api/access-mode",
        ApiAccessModeBody = "Geeft { mode, domain } voor de toegangsbeveiliging terug. Altijd toegankelijk zonder authenticatie zodat de login-pagina kan booten.",
        ApiLoginMethod = "POST /api/login",
        ApiLoginBody = "Body: { pin? } in PIN-modus, of { username, password } in AD-modus. Bij succes 200 + sessie-cookie. Bij faal 401.",
        ApiLogoutMethod = "POST /api/logout",
        ApiLogoutBody = "Beëindigt de huidige sessie en wist de cookie.",

        ApiIpcTitle = "Interne IPC (alleen voor beheerders)",
        ApiIpcBody = "Display.exe en WebHost.exe communiceren via de named-pipe \\\\.\\pipe\\InteractiveMask " +
                     "met length-prefixed JSON-envelopes. Niet bedoeld voor extern gebruik; de pipe-grens is per definitie een same-machine vertrouwensgrens. " +
                     "De pre-authenticated flag op een ToggleRequest mag dus alleen door de WebHost worden gezet.",

        FaqTitle = "Veelgestelde vragen",
        FaqQ1 = "Wat als ik mijn sessie-PIN vergeet?",
        FaqA1 = "Vraag een beheerder om de admin-PIN. Via Setup → Beheerder kan deze de sessie opnieuw initialiseren door alle maskers te verwijderen; daarna kunt u een nieuwe sessie-PIN kiezen.",
        FaqQ2 = "Wat als de NVR offline is?",
        FaqA2 = "De betreffende tegels worden rood en tonen “Verbinding verbroken”. De applicatie blijft proberen automatisch opnieuw verbinding te maken; u hoeft zelf niets te doen.",
        FaqQ3 = "Kan ik tegelijk lokaal en op afstand werken?",
        FaqA3 = "Ja. Een masker dat lokaal aangezet wordt is meteen zichtbaar in de browser, en omgekeerd. De PIN is voor beide kanten dezelfde.",
        FaqQ4 = "Komt mijn handeling in een logboek?",
        FaqA4 = "Ja. Iedere mask-actie en iedere PIN-poging wordt opgeslagen met tijdstip en bron (lokaal of op afstand). De beheerder kan dit logboek inzien op de Audit-tab.",
        FaqQ5 = "Heeft de masker invloed op de opname van de NVR?",
        FaqA5 = "Nee. Het masker is alleen op dit scherm zichtbaar. De NVR blijft normaal opnemen volgens de instellingen van uw locatie.",
        FaqQ6 = "Wat doet de oranje rand om een tegel?",
        FaqA6 = "Dat is de waarschuwing dat het masker bijna automatisch wordt opgeheven. Zie het hoofdstuk “Auto-uit timer”.",
    };

    public static readonly HelpStrings En = new()
    {
        WindowTitle  = "InteractiveMask — User guide",
        PageTitle    = "User guide",
        PageSubtitle = "How to use the camera screen and the privacy controls.",
        ButtonClose  = "Close",

        NavWelcome    = "Welcome",
        NavScreen     = "The screen",
        NavPrivacyOn  = "Activate privacy",
        NavPrivacyOff = "Deactivate privacy",
        NavAutoOff    = "Auto-off timer",
        NavRemote     = "Remote control",
        NavAdmin      = "For administrators",
        NavApi        = "API and integration",
        NavFaq        = "FAQ",

        WelcomeTitle = "Welcome",
        WelcomeIntro = "This application shows the live camera feeds of the ward in a single overview. " +
                       "One click on a tile blurs that feed beyond recognition — we call that a privacy mask.",
        WelcomeWhoFor = "Who is this for?",
        WelcomeWhoForBody = "Caregivers and assistants. No technical background is required. " +
                            "Cameras and network configuration live on a separate administrator page (see “For administrators”).",
        WelcomeWhatIsMaskTitle = "What is a privacy mask?",
        WelcomeWhatIsMaskBody = "A privacy mask covers a single camera tile with a heavy blur so detail can no longer be seen, while the NVR recorder keeps recording in the background. " +
                                "This protects the privacy of residents at moments when that is needed.",

        ScreenTitle = "The screen",
        ScreenIntro = "The main screen is a grid of camera tiles. Each tile shows one camera. " +
                      "The dot in the bottom-left corner tells you the state of that camera.",
        ScreenStatusTitle = "Status colours",
        ScreenStatusGreen = "Green — live image",
        ScreenStatusAmber = "Amber — connecting",
        ScreenStatusRed   = "Red — no signal or connection lost",
        ScreenEmptyTile   = "A tile that says “empty” means no camera is bound to this position yet.",
        ScreenLegendCaption = "Sample grid with the four states a tile can be in:",
        ScreenTileLive       = "Live",
        ScreenTileConnecting = "Connecting",
        ScreenTileLost       = "Lost",
        ScreenTileMasked     = "Mask active",

        PrivacyOnTitle = "Activate privacy",
        PrivacyOnIntro = "When you turn privacy on, the chosen tile blurs immediately. This happens on the screen only; the NVR keeps recording.",
        PrivacyOnSteps = "Click (or tap) the tile you want to obscure. The blur appears immediately and the tile shows “Privacy active”.",
        PrivacyOnFirstPin = "The first time you place a mask in a session, the system may ask you to choose a 4-digit session PIN. " +
                            "You will use that same PIN later to remove the masks.",
        PrivacyOnTip = "You can mask as many tiles at once as you like. Each click works independently of the others.",

        PrivacyOffTitle = "Deactivate privacy",
        PrivacyOffIntro = "Click a masked tile again to remove the mask. Depending on your site’s settings the system asks for a PIN or a Windows sign-in.",
        PrivacyOffPinFlow = "With session PIN: a keypad appears. Enter the four digits you chose when the first mask was placed.",
        PrivacyOffAdFlow = "With Windows sign-in: enter your username and password. Your name will appear in the audit log.",
        PrivacyOffLockoutTitle = "What if I type the PIN wrong too often?",
        PrivacyOffLockoutBody = "After three wrong attempts the keypad locks for 30 seconds. " +
                                "Wait for the timer to reach zero and try again calmly.",

        AutoOffTitle = "Auto-off timer",
        AutoOffIntro = "A privacy mask can expire automatically after a set number of minutes, so a mask can never be left on by accident for an entire day.",
        AutoOffWarningRing = "Just before the mask is removed automatically, the tile gets a pulsing amber ring and the text “auto-off”. That is your warning.",
        AutoOffNote = "A typical setup is auto-off = 5 minutes and warning = 2 minutes. The administrator can change this on the Privacy tab.",
        AutoOffPhaseStart   = "0 min – mask placed",
        AutoOffPhaseWarning = "3 min – amber ring",
        AutoOffPhaseDone    = "5 min – mask removed",

        RemoteTitle = "Remote control",
        RemoteIntro = "You can also operate the same tiles from a normal web browser — a tablet, another workstation, or a phone in the office.",
        RemoteUrlNote = "Open in the browser: http://<name-of-the-kiosk-pc>:8080. Your administrator will tell you the exact address for your site.",
        RemoteSamePin = "Clicking in the browser works exactly like clicking on the screen itself. Removing a mask asks for your PIN or sign-in in the browser too.",
        RemoteShared = "You can use both at the same time. The session PIN is shared between local and remote.",

        AdminTitle = "For administrators",
        AdminOpening = "Open the administrator section by right-clicking the main screen → Setup… → enter the admin PIN. " +
                       "Or use the button in this help window if you are already in setup.",
        AdminTabs = "Setup tabs: NVR (recorder connection), Cameras (which camera on which slot), Grid (grid size), Privacy (PIN policy and auto-off), Web-UI (ports and certificate), Audit log, and Administrator (admin PIN, language, kiosk mode).",
        AdminKiosk = "Kiosk mode blocks keys such as Alt+Tab, the Windows key and Alt+F4 so users cannot accidentally leave the application. The administrator can always return through right-click and the admin PIN.",
        AdminAd = "Caregiver identity (AD mode) replaces the session PIN keypad with a Windows sign-in. The audit log will then record exactly who removed each mask.",
        AdminAudit = "The Audit tab shows every mask action, PIN attempt and NVR connection with timestamp, source and any detail. Use “Export to CSV…” to keep or share the log.",
        AdminTechNote = "Technical note for administrators: the NVR password and certificate password are stored encrypted via Windows DPAPI at machine scope. " +
                        "Browser control is served by a built-in web server (WebHost) which the Web-UI tab can expose on the LAN or restrict to localhost.",

        ApiTitle = "API and integration",
        ApiIntro = "The WebHost component exposes a small, local REST API so a browser, tablet or another management system can perform the same actions as the kiosk itself. " +
                   "All endpoints accept and return JSON, except the snapshot endpoint which returns a JPEG binary.",
        ApiBaseTitle = "Base URL",
        ApiBaseBody = "http://<kiosk-host-name>:<HttpPort> — default port 8080. " +
                      "If configured, HTTPS is available on the separate port set in Setup → Web UI. " +
                      "By default the API only listens on localhost; check “Accessible on the LAN” in Setup to allow remote access.",
        ApiAuthTitle = "Authentication mode",
        ApiAuthBody = "The server decides which auth flow to use. /api/auth-mode returns \"pin\", \"ad\" or \"off\". " +
                      "In pin mode the client posts the session PIN with /api/toggle. In ad mode the client posts username + password; " +
                      "the WebHost validates locally via Windows LogonUser and forwards a pre-authenticated toggle to the Display side. " +
                      "Passwords therefore never travel across the IPC channel.",
        ApiEndpointsTitle = "Endpoints",

        ApiStateMethod = "GET /api/state",
        ApiStateBody = "Full snapshot of the grid and per-tile state (slot, label, masked, status, countdown). " +
                       "Used by the browser UI; polled once per second by default.",
        ApiAuthModeMethod = "GET /api/auth-mode",
        ApiAuthModeBody = "Returns { mode, domain }. mode = \"pin\" | \"ad\" | \"off\". domain is only populated in AD mode.",
        ApiToggleMethod = "POST /api/toggle",
        ApiToggleBody = "Body: { slot, pin?, username?, password? }. Result: Ok / PinRequired / PinWrong / LockedOut / CredentialsRequired / CredentialsWrong / InvalidSlot. " +
                        "In PIN mode a first click yields PinRequired; retry with the PIN. In AD mode an unmask yields CredentialsRequired; retry with username + password.",
        ApiSnapshotMethod = "GET /api/snapshot/{slot}",
        ApiSnapshotBody = "One-shot JPEG snapshot of the current frame (image/jpeg). " +
                          "200 = OK with JPEG bytes, 403 = privacy mask active (refused), 404 = no camera or no frame. " +
                          "Designed for manual refresh in a browser/tablet — not for periodic polling.",
        ApiAuditMethod = "GET /api/audit?limit=N",
        ApiAuditBody = "Tail of the audit log as a JSON array of NDJSON events. limit defaults to 100, max 5000.",

        ApiAccessTitle = "Web interface access protection",
        ApiAccessBody = "Independent of the per-tile PIN/AD policy, the web interface itself can sit behind sign-in. Configure under Setup → Web UI → Web interface access. Three modes: \"off\" (open, default), \"pin\" (shared access PIN, DPAPI-encrypted), \"ad\" (Windows credentials). When set to \"pin\" or \"ad\" the server returns 401 for unauthenticated API calls and redirects pages to /login. Sessions live 8 hours with a sliding window via an HttpOnly cookie.",
        ApiAccessModeMethod = "GET /api/access-mode",
        ApiAccessModeBody = "Returns { mode, domain } for web access. Always reachable without authentication so the login page can boot.",
        ApiLoginMethod = "POST /api/login",
        ApiLoginBody = "Body: { pin? } in PIN mode, or { username, password } in AD mode. On success 200 + session cookie. On failure 401.",
        ApiLogoutMethod = "POST /api/logout",
        ApiLogoutBody = "Ends the current session and clears the cookie.",

        ApiIpcTitle = "Internal IPC (administrators only)",
        ApiIpcBody = "Display.exe and WebHost.exe communicate over the named pipe \\\\.\\pipe\\InteractiveMask " +
                     "with length-prefixed JSON envelopes. Not intended for external use; the pipe boundary is by design a same-machine trust boundary. " +
                     "The pre-authenticated flag on a ToggleRequest may therefore only be set by the WebHost.",

        FaqTitle = "Frequently asked questions",
        FaqQ1 = "What if I forget my session PIN?",
        FaqA1 = "Ask an administrator for the admin PIN. From Setup → Administrator they can reset the session by clearing all masks; you can then choose a new session PIN.",
        FaqQ2 = "What if the NVR is offline?",
        FaqA2 = "The affected tiles turn red and show “Connection lost”. The application keeps trying to reconnect automatically; you do not need to do anything.",
        FaqQ3 = "Can I work locally and remotely at the same time?",
        FaqA3 = "Yes. A mask placed locally is instantly visible in the browser, and vice versa. The PIN is the same for both sides.",
        FaqQ4 = "Will my action show up in a log?",
        FaqA4 = "Yes. Every mask action and every PIN attempt is recorded with a timestamp and source (local or remote). The administrator can review the log on the Audit tab.",
        FaqQ5 = "Does a mask affect the NVR recording?",
        FaqA5 = "No. The mask is visible only on this screen. The NVR keeps recording according to your site’s settings.",
        FaqQ6 = "What is the amber ring on a tile?",
        FaqA6 = "It is the warning that the mask is about to be removed automatically. See the chapter “Auto-off timer”.",
    };
}
