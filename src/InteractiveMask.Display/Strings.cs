using System.ComponentModel;

namespace InteractiveMask.Display;

/// <summary>
/// Language-bound string provider. Implemented as an observable singleton so XAML
/// bindings via <c>{Binding Source={x:Static local:Strings.Instance}, Path=Current.X}</c>
/// auto-refresh when <see cref="Apply"/> is called at runtime; no app restart needed
/// for a language switch.
/// </summary>
public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Instance { get; } = new();

    /// <summary>
    /// Languages this build ships UI strings for. Order = order shown in the
    /// language picker (Setup admin tab + first-run dialog). DE/FR/ES are
    /// scaffolded in v1.3.0 item 4a; their strings start out as a copy of
    /// EN until item 4b lands the proper translations.
    /// </summary>
    public IReadOnlyList<LanguageChoice> SupportedLanguages { get; } = new[]
    {
        new LanguageChoice("nl", "Nederlands"),
        new LanguageChoice("en", "English"),
        new LanguageChoice("de", "Deutsch"),
        new LanguageChoice("fr", "Français"),
        new LanguageChoice("es", "Español"),
    };

    private StringsTable _current = StringsTable.Nl;
    private string _currentCode = "nl";

    public StringsTable Current
    {
        get => _current;
        private set
        {
            if (_current == value) return;
            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        }
    }

    public string LanguageCode => _currentCode;

    public void Apply(string? languageCode)
    {
        var code = (languageCode ?? "").Trim().ToLowerInvariant();
        var (table, normalised) = code switch
        {
            "nl"          => (StringsTable.Nl, "nl"),
            "en"          => (StringsTable.En, "en"),
            "de"          => (StringsTable.De, "de"),
            "fr"          => (StringsTable.Fr, "fr"),
            "es"          => (StringsTable.Es, "es"),
            _             => (StringsTable.Nl, "nl"),
        };
        _currentCode = normalised;
        Current = table;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Item shown in the language picker (Setup admin tab and the first-run
/// language dialog). <see cref="Code"/> matches what is persisted into
/// <c>AppSettings.Language</c>; <see cref="DisplayName"/> is shown in the
/// list and is intentionally not localised (the user picks based on the
/// native name of their own language).
/// </summary>
public sealed record LanguageChoice(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed class StringsTable
{
    // Tile overlay + badges
    public string PrivacyActive { get; init; } = "";
    public string AutoOff { get; init; } = "";
    public string PrivacyBadge { get; init; } = "";
    public string EmptyTile { get; init; } = "";

    // Tile status texts
    public string StatusConnecting { get; init; } = "";
    public string StatusDisconnected { get; init; } = "";
    public string StatusVideoLoss { get; init; } = "";
    public string StatusError { get; init; } = "";
    public string ErrorPrefix { get; init; } = "";

    // PIN dialog (session)
    public string PinSetTitle { get; init; } = "";
    public string PinSetSubtitle { get; init; } = "";
    public string PinVerifyTitle { get; init; } = "";
    public string PinVerifySubtitle { get; init; } = "";
    public string PinWrong { get; init; } = "";
    public string PinLockedOutFormat { get; init; } = "";
    public string PinHint4Digits { get; init; } = "";

    // Kiosk menu / admin
    public string MenuSetup { get; init; } = "";
    public string MenuExit { get; init; } = "";
    public string MenuHelp { get; init; } = "";
    public string AdminPinFirstSetupTitle { get; init; } = "";
    public string AdminPinFirstSetupSub { get; init; } = "";
    public string AdminPinExitTitle { get; init; } = "";
    public string AdminPinExitSub { get; init; } = "";
    public string AdminPinSetupOpenTitle { get; init; } = "";
    public string AdminPinSetupOpenSub { get; init; } = "";

    // Setup window — chrome / shared
    public string SetupTitle { get; init; } = "";
    public string SetupBrandSub { get; init; } = "";
    public string SetupCancel { get; init; } = "";
    public string SetupSave { get; init; } = "";
    public string SetupConfigLoaded { get; init; } = "";
    public string SetupChangesApplied { get; init; } = "";
    public string SetupRestartPrompt { get; init; } = "";

    // Setup window — sidebar nav labels
    public string NavNvr { get; init; } = "";
    public string NavCameras { get; init; } = "";
    public string NavGrid { get; init; } = "";
    public string NavPrivacy { get; init; } = "";
    public string NavWeb { get; init; } = "";
    public string NavAudit { get; init; } = "";
    public string NavAdmin { get; init; } = "";

    // NVR tab
    public string NvrPageTitle { get; init; } = "";
    public string NvrPageSubtitle { get; init; } = "";
    public string NvrCardConnection { get; init; } = "";
    public string NvrFieldIp { get; init; } = "";
    public string NvrFieldPort { get; init; } = "";
    public string NvrFieldUser { get; init; } = "";
    public string NvrFieldPassword { get; init; } = "";
    public string NvrPasswordHint { get; init; } = "";
    public string NvrAddRow { get; init; } = "";
    public string NvrDeleteRowTooltip { get; init; } = "";
    public string NvrEditRowTooltip { get; init; } = "";
    public string NvrHeaderName { get; init; } = "";
    public string NvrMultiHint { get; init; } = "";
    public string NvrDialogTitle { get; init; } = "";
    public string NvrDialogAddTitle { get; init; } = "";
    public string NvrDialogEditTitle { get; init; } = "";
    public string NvrDialogSubtitle { get; init; } = "";
    public string NvrDialogEditSubtitle { get; init; } = "";
    public string NvrDialogErrIpEmpty { get; init; } = "";

    // Cameras tab
    public string CamerasPageTitle { get; init; } = "";
    public string CamerasPageSubtitle { get; init; } = "";
    public string CamerasCardBindings { get; init; } = "";
    public string CamerasAddRow { get; init; } = "";
    public string CamerasHelpText { get; init; } = "";
    public string CamerasDeleteRowTooltip { get; init; } = "";
    public string CamerasReorderTooltip { get; init; } = "";
    public string StreamHigh { get; init; } = "";
    public string StreamDefault { get; init; } = "";
    public string StreamLow { get; init; } = "";
    public string CamerasHeaderSlot { get; init; } = "";
    public string CamerasHeaderNvr { get; init; } = "";
    public string CamerasHeaderCameraIndex { get; init; } = "";
    public string CamerasHeaderStream { get; init; } = "";
    public string CamerasHeaderLabel { get; init; } = "";

    // Grid tab
    public string GridPageTitle { get; init; } = "";
    public string GridPageSubtitle { get; init; } = "";
    public string GridCardSize { get; init; } = "";
    public string GridTilesCountFormat { get; init; } = "";
    public string GridChoice1Caption { get; init; } = "";
    public string GridChoice4Caption { get; init; } = "";
    public string GridChoice9Caption { get; init; } = "";
    public string GridChoice16Caption { get; init; } = "";

    // Privacy tab
    public string PrivacyPageTitle { get; init; } = "";
    public string PrivacyPageSubtitle { get; init; } = "";
    public string PrivacyCardSessionPin { get; init; } = "";
    public string PrivacySessionPinHelp { get; init; } = "";
    public string PrivacyCardAutoOff { get; init; } = "";
    public string PrivacyAutoOffMinutesLabel { get; init; } = "";
    public string PrivacyWarningMinutesLabel { get; init; } = "";
    public string PrivacyAutoOffExample { get; init; } = "";
    public string PrivacyCardMassMask { get; init; } = "";
    public string PrivacyCardMode { get; init; } = "";
    public string PrivacyModeOversightLabel { get; init; } = "";
    public string PrivacyModeOversightHelp { get; init; } = "";
    public string PrivacyModePrivacyLabel { get; init; } = "";
    public string PrivacyModePrivacyHelp { get; init; } = "";
    public string PrivacyDefaultRequireAuthOnRevealLabel { get; init; } = "";
    public string PrivacyDefaultRequireAuthOnRevealHelp { get; init; } = "";

    // Web-UI tab
    public string WebPageTitle { get; init; } = "";
    public string WebPageSubtitle { get; init; } = "";
    public string WebCardPorts { get; init; } = "";
    public string WebHttpPortLabel { get; init; } = "";
    public string WebHttpsPortLabel { get; init; } = "";
    public string WebCardLan { get; init; } = "";
    public string WebLanHelp { get; init; } = "";
    public string WebCardCert { get; init; } = "";
    public string WebCertIntro { get; init; } = "";
    public string WebCertPathLabel { get; init; } = "";
    public string WebCertBrowse { get; init; } = "";
    public string WebCertPasswordLabel { get; init; } = "";
    public string WebCertGenerate { get; init; } = "";
    public string WebCertRestartHint { get; init; } = "";

    // Web-UI access auth (page-gating)
    public string WebAccessCardTitle { get; init; } = "";
    public string WebAccessCardDescription { get; init; } = "";
    public string WebAccessMode { get; init; } = "";
    public string WebAccessModeOff { get; init; } = "";
    public string WebAccessModePin { get; init; } = "";
    public string WebAccessModeAd { get; init; } = "";
    public string WebAccessPin { get; init; } = "";
    public string WebAccessDomain { get; init; } = "";

    // Audit tab
    public string AuditPageTitle { get; init; } = "";
    public string AuditPageSubtitle { get; init; } = "";
    public string AuditCardRecent { get; init; } = "";
    public string AuditRefresh { get; init; } = "";
    public string AuditExportCsv { get; init; } = "";
    public string AuditHeaderTimestamp { get; init; } = "";
    public string AuditHeaderType { get; init; } = "";
    public string AuditHeaderSlot { get; init; } = "";
    public string AuditHeaderLabel { get; init; } = "";
    public string AuditHeaderSource { get; init; } = "";
    public string AuditHeaderDetail { get; init; } = "";

    // Audit forwarding (Syslog) sub-card
    public string AuditCardForwarding { get; init; } = "";
    public string AuditSyslogEnabled { get; init; } = "";
    public string AuditSyslogHost { get; init; } = "";
    public string AuditSyslogPort { get; init; } = "";
    public string AuditSyslogProtocol { get; init; } = "";
    public string AuditSyslogAppName { get; init; } = "";
    public string AuditSyslogFacility { get; init; } = "";
    public string AuditSyslogTest { get; init; } = "";
    public string AuditSyslogTesting { get; init; } = "";
    public string AuditSyslogTestOk { get; init; } = "";
    public string AuditSyslogTestFailFormat { get; init; } = "";
    public string AuditSyslogTestNoHost { get; init; } = "";

    // Admin (Beheerder) tab
    public string AdminPageTitle { get; init; } = "";
    public string AdminPageSubtitle { get; init; } = "";
    public string AdminCardPin { get; init; } = "";
    public string AdminPinDescription { get; init; } = "";
    public string AdminPinChangeButton { get; init; } = "";
    public string AdminCardLanguage { get; init; } = "";
    public string AdminLangNl { get; init; } = "";
    public string AdminLangEn { get; init; } = "";
    public string AdminLanguageHint { get; init; } = "";
    public string AdminCardIdentity { get; init; } = "";
    public string AdminIdentityDescription { get; init; } = "";
    public string AdminDomainLabel { get; init; } = "";
    public string AdminDomainHint { get; init; } = "";
    public string AdminCardKiosk { get; init; } = "";
    public string AdminKioskDescription { get; init; } = "";
    public string AdminCardHelp { get; init; } = "";
    public string AdminHelpDescription { get; init; } = "";
    public string AdminHelpOpen { get; init; } = "";

    // Setup validation / status messages
    public string SetupErrPort { get; init; } = "";
    public string SetupErrAutoMinutes { get; init; } = "";
    public string SetupErrWarningMinutes { get; init; } = "";
    public string SetupErrHttpPort { get; init; } = "";
    public string SetupErrHttpsPort { get; init; } = "";
    public string SetupErrDuplicateSlotFormat { get; init; } = "";
    public string SetupErrDuplicateCameraFormat { get; init; } = "";
    public string SetupErrDuplicateNvrFormat { get; init; } = "";
    public string SetupErrCameraOrphanFormat { get; init; } = "";
    public string SetupErrNvrInUseFormat { get; init; } = "";
    public string SetupErrNoNvr { get; init; } = "";
    public string SetupErrSaveFailedFormat { get; init; } = "";
    public string SetupCertCreatedFormat { get; init; } = "";
    public string SetupCertCreateFailedFormat { get; init; } = "";
    public string SetupAdminPinUpdated { get; init; } = "";
    public string SetupExportedFormat { get; init; } = "";
    public string SetupExportFailedFormat { get; init; } = "";
    public string SetupBrowseCertTitle { get; init; } = "";
    public string SetupBrowseCertFilter { get; init; } = "";
    public string SetupAuditExportTitle { get; init; } = "";
    public string SetupAuditExportFilter { get; init; } = "";
    public string SetupAdminPinNewTitle { get; init; } = "";
    public string SetupAdminPinNewSub { get; init; } = "";
    public string SetupAdLoginSubtitle { get; init; } = "";
    public string SetupAdRemoveTitle { get; init; } = "";

    // MainWindow status / dialogs
    public string MainNoConfigLine { get; init; } = "";

    // Reconnect banner
    public string ConnectionLost { get; init; } = "";
    public string ConnectionLostFormat { get; init; } = "";
    public string ConnectionAttempting { get; init; } = "";

    // Mass mask / unmask (long-press gesture, v1.3.0 item 1)
    public string MassUnmaskConfirmTitle { get; init; } = "";
    public string MassUnmaskConfirmBody { get; init; } = "";
    public string MassUnmaskAuthTitle { get; init; } = "";
    public string PrivacyShowMassUnmaskConfirmLabel { get; init; } = "";
    public string PrivacyShowMassUnmaskConfirmHelp { get; init; } = "";
    public string PrivacyLongPressHelp { get; init; } = "";

    // First-run language picker (v1.3.0 item 5)
    public string FirstRunLanguageTitle { get; init; } = "";
    public string FirstRunLanguageSubtitle { get; init; } = "";
    public string FirstRunLanguageContinue { get; init; } = "";

    public static readonly StringsTable Nl = new()
    {
        PrivacyActive             = "Privacy actief",
        AutoOff                   = "auto-uit",
        PrivacyBadge              = "• PRIVACY",
        EmptyTile                 = "leeg",

        StatusConnecting          = "Verbinden...",
        StatusDisconnected        = "Verbinding verbroken",
        StatusVideoLoss           = "Geen videosignaal",
        StatusError               = "Fout",
        ErrorPrefix               = "Fout: ",

        PinSetTitle               = "Stel sessie-PIN in",
        PinSetSubtitle            = "Deze PIN is nodig om privacy weer uit te zetten.",
        PinVerifyTitle            = "Voer sessie-PIN in",
        PinVerifySubtitle         = "Privacy uitzetten",
        PinWrong                  = "Onjuiste PIN. Probeer opnieuw.",
        PinLockedOutFormat        = "Te veel pogingen. Wacht {0} seconden.",
        PinHint4Digits            = "4 cijfers",

        MenuSetup                 = "Setup...",
        MenuExit                  = "Afsluiten",
        MenuHelp                  = "Help...",
        AdminPinFirstSetupTitle   = "Admin-PIN instellen",
        AdminPinFirstSetupSub     = "Eerste start: kies een 4-cijferige beheerderscode voor afsluiten en setup.",
        AdminPinExitTitle         = "Afsluiten",
        AdminPinExitSub           = "Voer admin-PIN in om de kiosk af te sluiten.",
        AdminPinSetupOpenTitle    = "Setup beheerder",
        AdminPinSetupOpenSub      = "Voer admin-PIN in om setup te openen.",

        SetupTitle                = "InteractiveMask — Setup",
        SetupBrandSub             = "Configureer NVR, cameras en privacy.",
        SetupCancel               = "Annuleer",
        SetupSave                 = "Opslaan",
        SetupConfigLoaded         = "Configuratie geladen",
        SetupChangesApplied       = "Wijzigingen toegepast.",
        SetupRestartPrompt        = "Voor wijzigingen in NVR-verbinding, camera-bindingen of grid-grootte is een herstart van Display nodig. Nu opnieuw starten?",

        NavNvr                    = "NVR",
        NavCameras                = "Cameras",
        NavGrid                   = "Grid",
        NavPrivacy                = "Privacy",
        NavWeb                    = "Web-UI",
        NavAudit                  = "Audit-log",
        NavAdmin                  = "Beheerder",

        NvrPageTitle              = "NVR-verbindingen",
        NvrPageSubtitle           = "Een of meerdere NVRs. Camera's in de Cameras-tab kunnen elke NVR uit deze lijst kiezen.",
        NvrCardConnection         = "NVR-lijst",
        NvrFieldIp                = "IP-adres",
        NvrFieldPort              = "Poort",
        NvrFieldUser              = "Gebruiker",
        NvrFieldPassword          = "Wachtwoord",
        NvrPasswordHint           = "Het wachtwoord wordt versleuteld opgeslagen via Windows DPAPI (machine-scope).",
        NvrAddRow                 = "NVR toevoegen",
        NvrDeleteRowTooltip       = "NVR verwijderen",
        NvrEditRowTooltip         = "NVR bewerken",
        NvrHeaderName             = "NAAM",
        NvrMultiHint              = "Voeg meerdere NVRs toe als je een grid wilt opbouwen met camera's uit verschillende recorders. Iedere camera in de Cameras-tab kiest welke NVR via de NVR-kolom.",
        NvrDialogTitle            = "NVR-verbinding",
        NvrDialogAddTitle         = "NVR toevoegen",
        NvrDialogEditTitle        = "NVR bewerken",
        NvrDialogSubtitle         = "Direct LAN, IDIS-protocol op poort 8016 (standaard).",
        NvrDialogEditSubtitle     = "Pas de verbindingsgegevens aan.",
        NvrDialogErrIpEmpty       = "IP-adres of hostnaam is verplicht.",

        CamerasPageTitle          = "Cameras",
        CamerasPageSubtitle       = "Welke camera's worden in welk slot van het grid getoond.",
        CamerasCardBindings       = "Slot-bindingen",
        CamerasAddRow             = "Rij toevoegen",
        CamerasHelpText           = "Slot = positie in het grid (1..N). Kies bij NVR uit welke recorder deze camera komt. Camera # = camerakanaal op die NVR (begint bij 1). Stream Normaal voor live multi-camera weergave; Hoog kost meer bandbreedte.",
        CamerasDeleteRowTooltip   = "Rij verwijderen",
        CamerasReorderTooltip     = "Versleep om de volgorde van de tegels te wijzigen",
        StreamHigh                = "Hoog",
        StreamDefault             = "Normaal",
        StreamLow                 = "Laag",
        CamerasHeaderSlot         = "SLOT",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "CAMERA #",
        CamerasHeaderStream       = "STREAM",
        CamerasHeaderLabel        = "LABEL",

        GridPageTitle             = "Grid",
        GridPageSubtitle          = "Hoeveel tegels worden er op het scherm getoond.",
        GridCardSize              = "Grid-grootte",
        GridTilesCountFormat      = "{0} tegels",
        GridChoice1Caption        = "1 tegel (volledig scherm)",
        GridChoice4Caption        = "4 tegels",
        GridChoice9Caption        = "9 tegels",
        GridChoice16Caption       = "16 tegels",

        PrivacyPageTitle          = "Privacy",
        PrivacyPageSubtitle       = "Sessie-PIN-beleid, auto-uit timer en visuele waarschuwing.",
        PrivacyCardSessionPin     = "Sessie-PIN voor verzorgers",
        PrivacySessionPinHelp     = "Wanneer ingeschakeld vraagt het systeem om een 4-cijferige PIN bij het uitzetten van een privacy-masker. Bij de eerste mask van een sessie wordt de PIN vastgelegd; alle daaropvolgende uit-acties in dezelfde sessie gebruiken dezelfde PIN. Wanneer uit, kan iedereen die het scherm bedient een masker direct uitschakelen.",
        PrivacyCardAutoOff        = "Auto-uit timer",
        PrivacyAutoOffMinutesLabel = "Auto-uit (minuten, 0 = uit)",
        PrivacyWarningMinutesLabel = "Waarschuwing voor auto-uit (minuten)",
        PrivacyAutoOffExample     = "Voorbeeld: bij auto-uit = 5 en waarschuwing = 2 verschijnt op een tegel die om 14:00 wordt gemaskeerd vanaf 14:03 een pulserende oranje rand; om 14:05 wordt het masker automatisch verwijderd.",
        PrivacyCardMassMask       = "Alles maskeren / vrijgeven (long-press)",
        PrivacyCardMode           = "Standaard zichtbaarheid",
        PrivacyModeOversightLabel = "Standaard zicht (klassieke zorg)",
        PrivacyModeOversightHelp  = "Tegels starten zichtbaar. De zorgmedewerker klikt op een tegel om de privacy te beschermen wanneer er zorg geleverd wordt. Een auto-uit timer zorgt dat een masker niet onbedoeld blijft staan.",
        PrivacyModePrivacyLabel   = "Standaard privé",
        PrivacyModePrivacyHelp    = "Tegels starten geblurd. De zorgmedewerker tikt om kort te onthullen voor verificatie. Een auto-rollback timer brengt de tegel automatisch weer naar geblurd. Geschikt voor kantoorruimtes, recepties of locaties met striktere privacy-defaults.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Auth vragen bij elke onthulling",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Alleen actief in standaard-privé modus. Wanneer aan, vraagt elke tap de PIN of AD-credentials voordat de tegel wordt onthuld. Wanneer uit (aanbevolen), is een onthulling vrij omdat de auto-rollback de privacy snel herstelt.",

        WebPageTitle              = "Web-UI",
        WebPageSubtitle           = "Poorten, certificaat en netwerk-zichtbaarheid van de browser-bediening.",
        WebCardPorts              = "Poorten",
        WebHttpPortLabel          = "HTTP-poort",
        WebHttpsPortLabel         = "HTTPS-poort (leeg = uit)",
        WebCardLan                = "Bereikbaarheid op het LAN",
        WebLanHelp                = "Wanneer uit, luistert de WebHost alleen op localhost (alleen vanaf deze machine bereikbaar). Wanneer aan, ook op het LAN-IP-adres zodat collega-werkstations of een tablet op afstand kunnen bedienen.",
        WebCardCert               = "HTTPS-certificaat",
        WebCertIntro              = "Pad naar een PFX-bestand. Het wachtwoord wordt versleuteld opgeslagen (DPAPI). Of klik 'Self-signed genereren' om een nieuw certificaat te maken in ProgramData.",
        WebCertPathLabel          = "PFX-bestand",
        WebCertBrowse             = "Bladeren...",
        WebCertPasswordLabel      = "Wachtwoord",
        WebCertGenerate           = "Self-signed certificaat genereren",
        WebCertRestartHint        = "Wijzigingen aan poorten of certificaat treden pas in werking nadat de WebHost-service herstart is.",

        WebAccessCardTitle        = "Toegang tot de web-interface",
        WebAccessCardDescription  = "Bepaal of de web-interface vrij toegankelijk is, of dat eerst aanmelden vereist is. Dit staat los van het PIN/AD-beleid voor het uitzetten van een privacy-masker.",
        WebAccessMode             = "Toegangsmodus",
        WebAccessModeOff          = "Open (geen aanmelding)",
        WebAccessModePin          = "Gedeelde toegangs-PIN",
        WebAccessModeAd           = "Windows-aanmelding",
        WebAccessPin              = "Toegangs-PIN",
        WebAccessDomain           = "Standaard domein (optioneel)",

        AuditPageTitle            = "Audit-log",
        AuditPageSubtitle         = "Volledige geschiedenis van mask-acties, PIN-gebeurtenissen en NVR-verbindingen.",
        AuditCardRecent           = "Recente events",
        AuditRefresh              = "Vernieuwen",
        AuditExportCsv            = "Export naar CSV...",
        AuditHeaderTimestamp      = "TIJDSTIP",
        AuditHeaderType           = "TYPE",
        AuditHeaderSlot           = "SLOT",
        AuditHeaderLabel          = "LABEL",
        AuditHeaderSource         = "BRON",
        AuditHeaderDetail         = "DETAIL",

        AuditCardForwarding       = "Syslog-doorgifte",
        AuditSyslogEnabled        = "Audit-events naar externe SIEM/syslog-server doorsturen",
        AuditSyslogHost           = "Host",
        AuditSyslogPort           = "Poort",
        AuditSyslogProtocol       = "Protocol",
        AuditSyslogAppName        = "App-naam (RFC 5424)",
        AuditSyslogFacility       = "Facility (0-23)",
        AuditSyslogTest           = "Test bericht versturen",
        AuditSyslogTesting        = "Bezig met versturen...",
        AuditSyslogTestOk         = "Test-bericht verstuurd.",
        AuditSyslogTestFailFormat = "Versturen mislukt: {0}",
        AuditSyslogTestNoHost     = "Vul eerst een host in.",

        AdminPageTitle            = "Beheerder",
        AdminPageSubtitle         = "Toegang voor afsluiten en setup, en taalvoorkeur.",
        AdminCardPin              = "Admin-PIN",
        AdminPinDescription       = "Wordt gevraagd bij afsluiten van de kiosk en bij openen van setup. Vier cijfers.",
        AdminPinChangeButton      = "Nieuwe admin-PIN instellen...",
        AdminCardLanguage         = "Taal van de gebruikersinterface",
        AdminLangNl               = "Nederlands",
        AdminLangEn               = "English",
        AdminLanguageHint         = "Wijzigingen worden direct toegepast op alle weergegeven teksten.",
        AdminCardIdentity         = "Identiteit verzorgers",
        AdminIdentityDescription  = "Wanneer ingeschakeld vraagt het systeem om een Windows-aanmelding (LogonUser) bij elke unmask in plaats van een gedeelde sessie-PIN. Het audit-log toont de gebruikersnaam van degene die het mask heeft uitgezet — zinvol voor verantwoording in de zorg.",
        AdminDomainLabel          = "Standaard domein (optioneel)",
        AdminDomainHint           = "Laat leeg om de huidige machine / domain controller te gebruiken. Verzorgers kunnen ook DOMAIN\\gebruiker of gebruiker@domein typen.",
        AdminCardKiosk            = "Kiosk-modus",
        AdminKioskDescription     = "Wanneer ingeschakeld blokkeert het systeem Win-key, Alt+Tab, Alt+F4, Alt+Esc en Ctrl+Esc, en blijft het Display-venster boven alle andere vensters. Beheerder kan de kiosk altijd verlaten via rechter-muisklik en admin-PIN. Ctrl+Alt+Del kan in user-mode niet worden geblokkeerd; gebruik Group Policy op de host.",
        AdminCardHelp             = "Help",
        AdminHelpDescription      = "Open de ingebouwde handleiding voor verzorgers. Handig om door te lopen bij eerste ingebruikname of bij het instrueren van nieuwe collega's.",
        AdminHelpOpen             = "Open handleiding",

        SetupErrPort              = "Poort moet een getal zijn.",
        SetupErrAutoMinutes       = "Auto-uit timer moet 0 of hoger zijn.",
        SetupErrWarningMinutes    = "Waarschuwing-minuten moet 0 of hoger zijn.",
        SetupErrHttpPort          = "HTTP-poort moet een positief getal zijn.",
        SetupErrHttpsPort         = "HTTPS-poort moet een positief getal zijn (of leeg voor uit).",
        SetupErrDuplicateSlotFormat   = "Slot {0} is meer dan één keer gebruikt. Iedere positie mag maar één camera hebben.",
        SetupErrDuplicateCameraFormat = "Camera {0} staat meer dan één keer in de lijst. Iedere camera mag maar op één positie staan.",
        SetupErrDuplicateNvrFormat    = "NVR-id {0} komt meer dan één keer voor. Iedere NVR moet een uniek id hebben.",
        SetupErrCameraOrphanFormat    = "Slot {0} verwijst naar een NVR die niet (meer) in de lijst staat. Selecteer een geldige NVR.",
        SetupErrNvrInUseFormat        = "NVR \"{0}\" is in gebruik door een of meer cameras. Verplaats die cameras eerst naar een andere NVR voordat je deze NVR verwijdert.",
        SetupErrNoNvr                 = "Voeg minstens één NVR toe voordat je opslaat.",
        SetupErrSaveFailedFormat  = "Opslaan mislukt: {0}",
        SetupCertCreatedFormat    = "Self-signed certificaat aangemaakt: {0}",
        SetupCertCreateFailedFormat = "Kon certificaat niet aanmaken: {0}",
        SetupAdminPinUpdated      = "Admin-PIN bijgewerkt.",
        SetupExportedFormat       = "Geëxporteerd: {0}",
        SetupExportFailedFormat   = "Export mislukt: {0}",
        SetupBrowseCertTitle      = "Selecteer PFX-bestand",
        SetupBrowseCertFilter     = "Certificate (*.pfx;*.p12)|*.pfx;*.p12|Alle bestanden (*.*)|*.*",
        SetupAuditExportTitle     = "Audit-log exporteren",
        SetupAuditExportFilter    = "CSV (*.csv)|*.csv",
        SetupAdminPinNewTitle     = "Nieuwe admin-PIN",
        SetupAdminPinNewSub       = "Stel een nieuwe 4-cijferige beheerderscode in.",
        SetupAdLoginSubtitle      = "Meld je aan met je Windows-account.",
        SetupAdRemoveTitle        = "Privacy uitzetten",

        MainNoConfigLine          = "NVR-config ontbreekt. Rechter-muisklik voor Setup / Afsluiten.",

        ConnectionLost            = "Verbinding met NVR verloren",
        ConnectionLostFormat      = "{0} — opnieuw verbinden over {1} s...",
        ConnectionAttempting      = "Verbinden met NVR...",

        MassUnmaskConfirmTitle    = "Privacy van alle bewoners opheffen?",
        MassUnmaskConfirmBody     = "Met deze actie worden alle privacy-maskers tegelijk verwijderd. Druk op OK om door te gaan, of op Annuleren om elke tegel handmatig te beheren.",
        MassUnmaskAuthTitle       = "Alles vrijgeven",
        PrivacyShowMassUnmaskConfirmLabel = "Bevestiging tonen voordat alles vrijgegeven wordt",
        PrivacyShowMassUnmaskConfirmHelp  = "Toont een dialoog voordat een long-press alle privacy-maskers tegelijk opheft. Aanbevolen op installaties waar de auth-modus uit staat.",
        PrivacyLongPressHelp      = "Houd de muisknop een halve seconde ingedrukt op het scherm om alle tegels in een keer te maskeren of vrij te geven. Single-tap blijft individueel werken.",

        FirstRunLanguageTitle     = "Kies je taal",
        FirstRunLanguageSubtitle  = "Choose your language / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Doorgaan",
    };

    public static readonly StringsTable En = new()
    {
        PrivacyActive             = "Privacy active",
        AutoOff                   = "auto-off",
        PrivacyBadge              = "• PRIVACY",
        EmptyTile                 = "empty",

        StatusConnecting          = "Connecting...",
        StatusDisconnected        = "Connection lost",
        StatusVideoLoss           = "No video signal",
        StatusError               = "Error",
        ErrorPrefix               = "Error: ",

        PinSetTitle               = "Set session PIN",
        PinSetSubtitle            = "This PIN is required to disable privacy.",
        PinVerifyTitle            = "Enter session PIN",
        PinVerifySubtitle         = "Disable privacy",
        PinWrong                  = "Incorrect PIN. Try again.",
        PinLockedOutFormat        = "Too many attempts. Wait {0} seconds.",
        PinHint4Digits            = "4 digits",

        MenuSetup                 = "Setup...",
        MenuExit                  = "Exit",
        MenuHelp                  = "Help...",
        AdminPinFirstSetupTitle   = "Set admin PIN",
        AdminPinFirstSetupSub     = "First start: choose a 4-digit admin code for exit and setup.",
        AdminPinExitTitle         = "Exit",
        AdminPinExitSub           = "Enter admin PIN to exit the kiosk.",
        AdminPinSetupOpenTitle    = "Setup",
        AdminPinSetupOpenSub      = "Enter admin PIN to open setup.",

        SetupTitle                = "InteractiveMask — Setup",
        SetupBrandSub             = "Configure NVR, cameras and privacy.",
        SetupCancel               = "Cancel",
        SetupSave                 = "Save",
        SetupConfigLoaded         = "Configuration loaded",
        SetupChangesApplied       = "Changes applied.",
        SetupRestartPrompt        = "Changes to NVR connection, camera bindings or grid size require a Display restart. Restart now?",

        NavNvr                    = "NVR",
        NavCameras                = "Cameras",
        NavGrid                   = "Grid",
        NavPrivacy                = "Privacy",
        NavWeb                    = "Web UI",
        NavAudit                  = "Audit log",
        NavAdmin                  = "Administrator",

        NvrPageTitle              = "NVR connections",
        NvrPageSubtitle           = "One or multiple NVRs. Cameras on the Cameras tab can pick any NVR from this list.",
        NvrCardConnection         = "NVR list",
        NvrFieldIp                = "IP address",
        NvrFieldPort              = "Port",
        NvrFieldUser              = "Username",
        NvrFieldPassword          = "Password",
        NvrPasswordHint           = "The password is stored encrypted via Windows DPAPI (machine scope).",
        NvrAddRow                 = "Add NVR",
        NvrDeleteRowTooltip       = "Delete NVR",
        NvrEditRowTooltip         = "Edit NVR",
        NvrHeaderName             = "NAME",
        NvrMultiHint              = "Add multiple NVRs to build a grid with cameras from different recorders. Each camera on the Cameras tab picks its NVR via the NVR column.",
        NvrDialogTitle            = "NVR connection",
        NvrDialogAddTitle         = "Add NVR",
        NvrDialogEditTitle        = "Edit NVR",
        NvrDialogSubtitle         = "Direct LAN, IDIS protocol on port 8016 (default).",
        NvrDialogEditSubtitle     = "Update the connection details.",
        NvrDialogErrIpEmpty       = "IP address or hostname is required.",

        CamerasPageTitle          = "Cameras",
        CamerasPageSubtitle       = "Which cameras are shown in which slot of the grid.",
        CamerasCardBindings       = "Slot bindings",
        CamerasAddRow             = "Add row",
        CamerasHelpText           = "Slot = position in the grid (1..N). Pick at NVR which recorder this camera comes from. Camera # = camera channel on that NVR (starts at 1). Pick Default at Stream for live multi-camera viewing; High costs more bandwidth.",
        CamerasDeleteRowTooltip   = "Delete row",
        CamerasReorderTooltip     = "Drag to reorder the tile sequence",
        StreamHigh                = "High",
        StreamDefault             = "Default",
        StreamLow                 = "Low",
        CamerasHeaderSlot         = "SLOT",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "CAMERA #",
        CamerasHeaderStream       = "STREAM",
        CamerasHeaderLabel        = "LABEL",

        GridPageTitle             = "Grid",
        GridPageSubtitle          = "How many tiles are shown on screen.",
        GridCardSize              = "Grid size",
        GridTilesCountFormat      = "{0} tiles",
        GridChoice1Caption        = "1 tile (full-screen)",
        GridChoice4Caption        = "4 tiles",
        GridChoice9Caption        = "9 tiles",
        GridChoice16Caption       = "16 tiles",

        PrivacyPageTitle          = "Privacy",
        PrivacyPageSubtitle       = "Session-PIN policy, auto-off timer and visual warning.",
        PrivacyCardSessionPin     = "Session PIN for caregivers",
        PrivacySessionPinHelp     = "When enabled, the system asks for a 4-digit PIN to disable a privacy mask. The first mask of a session sets the PIN; subsequent off-actions in the same session use the same PIN. When off, anyone using the screen can disable a mask immediately.",
        PrivacyCardAutoOff        = "Auto-off timer",
        PrivacyAutoOffMinutesLabel = "Auto-off (minutes, 0 = disabled)",
        PrivacyWarningMinutesLabel = "Warning before auto-off (minutes)",
        PrivacyAutoOffExample     = "Example: with auto-off = 5 and warning = 2, a tile masked at 14:00 shows a pulsing orange border from 14:03; the mask is removed automatically at 14:05.",
        PrivacyCardMassMask       = "Mask / unmask everything (long-press)",
        PrivacyCardMode           = "Default visibility",
        PrivacyModeOversightLabel = "Oversight default (classic care)",
        PrivacyModeOversightHelp  = "Tiles start visible. A caregiver taps a tile to apply privacy when delivering care. An auto-off timer makes sure a mask is not left behind by accident.",
        PrivacyModePrivacyLabel   = "Privacy default",
        PrivacyModePrivacyHelp    = "Tiles start blurred. A caregiver taps to briefly reveal for verification. An auto-rollback timer takes the tile back to blurred. Fits office spaces, receptions, or sites with stricter privacy defaults.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Require auth on every reveal",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Only active in privacy-default mode. When on, every tap goes through the PIN or AD prompt before the tile is revealed. When off (recommended), a reveal is free because the auto-rollback restores privacy quickly.",

        WebPageTitle              = "Web UI",
        WebPageSubtitle           = "Ports, certificate and network reachability for browser control.",
        WebCardPorts              = "Ports",
        WebHttpPortLabel          = "HTTP port",
        WebHttpsPortLabel         = "HTTPS port (empty = off)",
        WebCardLan                = "LAN reachability",
        WebLanHelp                = "When off, the WebHost only listens on localhost (this machine only). When on, it also listens on the LAN IP so colleague workstations or a tablet can control remotely.",
        WebCardCert               = "HTTPS certificate",
        WebCertIntro              = "Path to a PFX file. The password is stored encrypted (DPAPI). Or click 'Generate self-signed' to create a new certificate in ProgramData.",
        WebCertPathLabel          = "PFX file",
        WebCertBrowse             = "Browse...",
        WebCertPasswordLabel      = "Password",
        WebCertGenerate           = "Generate self-signed certificate",
        WebCertRestartHint        = "Changes to ports or certificate take effect only after the WebHost service is restarted.",

        WebAccessCardTitle        = "Web interface access",
        WebAccessCardDescription  = "Choose whether the web interface is openly accessible, or whether sign-in is required first. This is independent of the per-tile PIN / AD policy used to disable a privacy mask.",
        WebAccessMode             = "Access mode",
        WebAccessModeOff          = "Open (no sign-in)",
        WebAccessModePin          = "Shared access PIN",
        WebAccessModeAd           = "Windows sign-in",
        WebAccessPin              = "Access PIN",
        WebAccessDomain           = "Default domain (optional)",

        AuditPageTitle            = "Audit log",
        AuditPageSubtitle         = "Full history of mask actions, PIN events and NVR connections.",
        AuditCardRecent           = "Recent events",
        AuditRefresh              = "Refresh",
        AuditExportCsv            = "Export to CSV...",
        AuditHeaderTimestamp      = "TIMESTAMP",
        AuditHeaderType           = "TYPE",
        AuditHeaderSlot           = "SLOT",
        AuditHeaderLabel          = "LABEL",
        AuditHeaderSource         = "SOURCE",
        AuditHeaderDetail         = "DETAIL",

        AuditCardForwarding       = "Syslog forwarding",
        AuditSyslogEnabled        = "Forward audit events to an external SIEM / syslog server",
        AuditSyslogHost           = "Host",
        AuditSyslogPort           = "Port",
        AuditSyslogProtocol       = "Protocol",
        AuditSyslogAppName        = "App name (RFC 5424)",
        AuditSyslogFacility       = "Facility (0-23)",
        AuditSyslogTest           = "Send test message",
        AuditSyslogTesting        = "Sending...",
        AuditSyslogTestOk         = "Test message sent.",
        AuditSyslogTestFailFormat = "Send failed: {0}",
        AuditSyslogTestNoHost     = "Enter a host first.",

        AdminPageTitle            = "Administrator",
        AdminPageSubtitle         = "Access for exit and setup, and language preference.",
        AdminCardPin              = "Admin PIN",
        AdminPinDescription       = "Required when exiting the kiosk and opening setup. Four digits.",
        AdminPinChangeButton      = "Set new admin PIN...",
        AdminCardLanguage         = "User-interface language",
        AdminLangNl               = "Dutch",
        AdminLangEn               = "English",
        AdminLanguageHint         = "Changes are applied immediately to all visible texts.",
        AdminCardIdentity         = "Caregiver identity",
        AdminIdentityDescription  = "When enabled, the system prompts for Windows credentials (LogonUser) on every unmask instead of a shared session PIN. The audit log records the username of whoever disabled the mask — useful for accountability in healthcare.",
        AdminDomainLabel          = "Default domain (optional)",
        AdminDomainHint           = "Leave empty to use the current machine / domain controller. Caregivers can also type DOMAIN\\user or user@domain.",
        AdminCardKiosk            = "Kiosk mode",
        AdminKioskDescription     = "When enabled, the system blocks Win key, Alt+Tab, Alt+F4, Alt+Esc and Ctrl+Esc, and keeps the Display window above all others. The administrator can always exit via right-click and admin PIN. Ctrl+Alt+Del cannot be blocked in user mode; use Group Policy on the host.",
        AdminCardHelp             = "Help",
        AdminHelpDescription      = "Open the built-in manual for caregivers. Useful when first using the system or when training new colleagues.",
        AdminHelpOpen             = "Open manual",

        SetupErrPort              = "Port must be a number.",
        SetupErrAutoMinutes       = "Auto-off timer must be 0 or higher.",
        SetupErrWarningMinutes    = "Warning minutes must be 0 or higher.",
        SetupErrHttpPort          = "HTTP port must be a positive number.",
        SetupErrHttpsPort         = "HTTPS port must be a positive number (or empty for off).",
        SetupErrDuplicateSlotFormat   = "Slot {0} is used more than once. Each grid position can hold only one camera.",
        SetupErrDuplicateCameraFormat = "Camera {0} appears more than once. Each camera can be placed on only one position.",
        SetupErrDuplicateNvrFormat    = "NVR id {0} appears more than once. Each NVR must have a unique id.",
        SetupErrCameraOrphanFormat    = "Slot {0} references an NVR that is no longer in the list. Pick a valid NVR.",
        SetupErrNvrInUseFormat        = "NVR \"{0}\" is in use by one or more cameras. Move those cameras to another NVR before deleting this one.",
        SetupErrNoNvr                 = "Add at least one NVR before saving.",
        SetupErrSaveFailedFormat  = "Save failed: {0}",
        SetupCertCreatedFormat    = "Self-signed certificate created: {0}",
        SetupCertCreateFailedFormat = "Could not create certificate: {0}",
        SetupAdminPinUpdated      = "Admin PIN updated.",
        SetupExportedFormat       = "Exported: {0}",
        SetupExportFailedFormat   = "Export failed: {0}",
        SetupBrowseCertTitle      = "Select PFX file",
        SetupBrowseCertFilter     = "Certificate (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*",
        SetupAuditExportTitle     = "Export audit log",
        SetupAuditExportFilter    = "CSV (*.csv)|*.csv",
        SetupAdminPinNewTitle     = "New admin PIN",
        SetupAdminPinNewSub       = "Set a new 4-digit admin code.",
        SetupAdLoginSubtitle      = "Sign in with your Windows account.",
        SetupAdRemoveTitle        = "Disable privacy",

        MainNoConfigLine          = "NVR config missing. Right-click for Setup / Exit.",

        ConnectionLost            = "NVR connection lost",
        ConnectionLostFormat      = "{0} — reconnecting in {1} s...",
        ConnectionAttempting      = "Connecting to NVR...",

        MassUnmaskConfirmTitle    = "Lift privacy on all residents?",
        MassUnmaskConfirmBody     = "This action removes every privacy mask at once. Press OK to continue, or Cancel to manage each tile individually.",
        MassUnmaskAuthTitle       = "Unmask all",
        PrivacyShowMassUnmaskConfirmLabel = "Show a confirmation before unmasking everyone",
        PrivacyShowMassUnmaskConfirmHelp  = "Adds a dialog before a long-press lifts every privacy mask at once. Recommended when authentication is disabled.",
        PrivacyLongPressHelp      = "Hold the mouse button down anywhere on the screen for half a second to mask or unmask every tile at once. Single-tap still toggles tiles individually.",

        FirstRunLanguageTitle     = "Pick your language",
        FirstRunLanguageSubtitle  = "Choose your language / Kies je taal / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Continue",
    };

    // ------------------------------------------------------------------
    // v1.3.0 item 4a scaffolding for German, French, Spanish.
    //
    // These tables intentionally point at the English content for now so
    // the picker is functional end-to-end (the user can switch to "Deutsch"
    // in Setup, the install-time picker can persist "de", every UI element
    // resolves and renders something) while we wait for item 4b to land
    // the actual native translations.
    //
    // When the translation pass lands, replace each `= En;` with a fully
    // populated `new() { ... }` block that mirrors En's shape, and the
    // assignment will hot-swap without any other code change.
    // ------------------------------------------------------------------

    public static readonly StringsTable De = En;
    public static readonly StringsTable Fr = En;
    public static readonly StringsTable Es = En;
}
