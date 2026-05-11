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
    public string SetupApply { get; init; } = "";
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
    public string CamerasSyncNames { get; init; } = "";
    public string CamerasSyncNamesProgress { get; init; } = "";
    public string CamerasSyncNamesDoneFormat { get; init; } = "";
    public string CamerasSyncNamesNoneApplied { get; init; } = "";
    public string CamerasSyncNamesFailFormat { get; init; } = "";
    public string CamerasSyncNamesNoCameras { get; init; } = "";
    public string StreamHigh { get; init; } = "";
    public string StreamDefault { get; init; } = "";
    public string StreamLow { get; init; } = "";
    public string CamerasHeaderSlot { get; init; } = "";
    public string CamerasHeaderNvr { get; init; } = "";
    public string CamerasHeaderCameraIndex { get; init; } = "";
    public string CamerasHeaderStream { get; init; } = "";
    public string CamerasHeaderLabel { get; init; } = "";
    public string CamerasHeaderNvrTitle { get; init; } = "";

    // Grid tab
    public string GridPageTitle { get; init; } = "";
    public string GridPageSubtitle { get; init; } = "";
    public string GridCardSize { get; init; } = "";
    public string GridTilesCountFormat { get; init; } = "";
    public string GridChoice1Caption { get; init; } = "";
    public string GridChoice4Caption { get; init; } = "";
    public string GridChoice9Caption { get; init; } = "";
    public string GridChoice16Caption { get; init; } = "";
    public string GridChoice25Caption { get; init; } = "";

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
    public string PrivacyCardTileOverlay { get; init; } = "";
    public string PrivacyShowCameraLabel { get; init; } = "";
    public string PrivacyShowCameraLabelHelp { get; init; } = "";
    public string PrivacyShowNvrTitle { get; init; } = "";
    public string PrivacyShowNvrTitleHelp { get; init; } = "";

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

    // About tab (v1.3.0)
    public string NavAbout { get; init; } = "";
    public string AboutPageTitle { get; init; } = "";
    public string AboutPageSubtitle { get; init; } = "";
    public string AboutCardProduct { get; init; } = "";
    public string AboutVersionLabel { get; init; } = "";
    public string AboutPublisherLabel { get; init; } = "";
    public string AboutCopyrightLabel { get; init; } = "";
    public string AboutWebsiteLabel { get; init; } = "";
    public string AboutDescription { get; init; } = "";
    public string AboutCardLicensesThird { get; init; } = "";
    public string AboutCardLicensesIntegrations { get; init; } = "";
    public string AboutCardBugReport { get; init; } = "";
    public string AboutBugReportIntro { get; init; } = "";
    public string AboutBugReportButton { get; init; } = "";
    public string AboutCardSystemCapabilities { get; init; } = "";
    public string AboutCapsRefreshButton { get; init; } = "";
    public string AboutCapsProbing { get; init; } = "";
    public string AboutCapsCpu { get; init; } = "";
    public string AboutCapsMemory { get; init; } = "";
    public string AboutCapsGpu { get; init; } = "";
    public string AboutCapsNpu { get; init; } = "";
    public string AboutCapsAiTier { get; init; } = "";
    public string AboutCapsNone { get; init; } = "";
    public string AboutCapsIntegrated { get; init; } = "";

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
        SetupApply                = "Toepassen",
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
        CamerasSyncNames          = "Namen ophalen vanuit NVR",
        CamerasSyncNamesProgress  = "Bezig met ophalen namen vanuit NVR...",
        CamerasSyncNamesDoneFormat = "Gesynchroniseerd: {0} namen bijgewerkt vanuit de NVR.",
        CamerasSyncNamesNoneApplied = "Synchronisatie voltooid: alle labels zijn handmatig aangepast en blijven ongewijzigd.",
        CamerasSyncNamesFailFormat = "Synchronisatie mislukt: {0}",
        CamerasSyncNamesNoCameras = "Voeg eerst camera-rijen toe.",
        StreamHigh                = "Hoog",
        StreamDefault             = "Normaal",
        StreamLow                 = "Laag",
        CamerasHeaderSlot         = "SLOT",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "CAMERA #",
        CamerasHeaderStream       = "STREAM",
        CamerasHeaderNvrTitle     = "NVR-TITEL",
        CamerasHeaderLabel        = "EIGEN LABEL",

        GridPageTitle             = "Grid",
        GridPageSubtitle          = "Hoeveel tegels worden er op het scherm getoond.",
        GridCardSize              = "Grid-grootte",
        GridTilesCountFormat      = "{0} tegels",
        GridChoice1Caption        = "1 tegel (volledig scherm)",
        GridChoice4Caption        = "4 tegels",
        GridChoice9Caption        = "9 tegels",
        GridChoice16Caption       = "16 tegels",
        GridChoice25Caption       = "25 tegels",

        PrivacyPageTitle          = "Privacy",
        PrivacyPageSubtitle       = "Sessie-PIN-beleid, auto-uit timer en visuele waarschuwing.",
        PrivacyCardSessionPin     = "Sessie-PIN voor operators",
        PrivacySessionPinHelp     = "Wanneer ingeschakeld vraagt het systeem om een 4-cijferige PIN bij het uitzetten van een privacy-masker. Bij de eerste mask van een sessie wordt de PIN vastgelegd; alle daaropvolgende uit-acties in dezelfde sessie gebruiken dezelfde PIN. Wanneer uit, kan iedereen die het scherm bedient een masker direct uitschakelen.",
        PrivacyCardAutoOff        = "Auto-uit timer",
        PrivacyAutoOffMinutesLabel = "Auto-uit (minuten, 0 = uit)",
        PrivacyWarningMinutesLabel = "Waarschuwing voor auto-uit (minuten)",
        PrivacyAutoOffExample     = "Voorbeeld: bij auto-uit = 5 en waarschuwing = 2 verschijnt op een tegel die om 14:00 wordt gemaskeerd vanaf 14:03 een pulserende oranje rand; om 14:05 wordt het masker automatisch verwijderd.",
        PrivacyCardMassMask       = "Alles maskeren / vrijgeven (long-press)",
        PrivacyCardMode           = "Standaard zichtbaarheid",
        PrivacyModeOversightLabel = "Standaard zicht",
        PrivacyModeOversightHelp  = "Tegels starten zichtbaar. De operator klikt op een tegel om de privacy te activeren op het moment dat dat nodig is. Een auto-uit timer zorgt dat een masker niet onbedoeld blijft staan.",
        PrivacyModePrivacyLabel   = "Standaard privé",
        PrivacyModePrivacyHelp    = "Tegels starten geblurd. De operator tikt om kort te onthullen voor verificatie. Een auto-rollback timer brengt de tegel automatisch weer naar geblurd. Geschikt voor kantoorruimtes, recepties, hotels, scholen, zorginstellingen of elke andere locatie met striktere privacy-defaults.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Auth vragen bij elke onthulling",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Alleen actief in standaard-privé modus. Wanneer aan, vraagt elke tap de PIN of AD-credentials voordat de tegel wordt onthuld. Wanneer uit (aanbevolen), is een onthulling vrij omdat de auto-rollback de privacy snel herstelt.",
        PrivacyCardTileOverlay    = "Tegel-overlay",
        PrivacyShowCameraLabel    = "Aangepaste naam tonen",
        PrivacyShowCameraLabelHelp = "Toont de naam die de beheerder zelf in de Cameras-tab heeft ingevuld, rechts in de overlay-balk onder de tegel.",
        PrivacyShowNvrTitle       = "NVR-titel tonen",
        PrivacyShowNvrTitleHelp   = "Toont de oorspronkelijke camera-titel zoals die op de NVR is geconfigureerd, links in de overlay-balk onder de tegel. Beide opties kunnen tegelijk aan staan; NVR-titel staat dan links, custom label rechts.",

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
        AdminCardIdentity         = "Identiteit operators",
        AdminIdentityDescription  = "Wanneer ingeschakeld vraagt het systeem om een Windows-aanmelding (LogonUser) bij elke unmask in plaats van een gedeelde sessie-PIN. Het audit-log toont de gebruikersnaam van degene die het mask heeft uitgezet — zinvol voor verantwoording en compliance.",
        AdminDomainLabel          = "Standaard domein (optioneel)",
        AdminDomainHint           = "Laat leeg om de huidige machine / domain controller te gebruiken. Operators kunnen ook DOMAIN\\gebruiker of gebruiker@domein typen.",
        AdminCardKiosk            = "Kiosk-modus",
        AdminKioskDescription     = "Wanneer ingeschakeld blokkeert het systeem Win-key, Alt+Tab, Alt+F4, Alt+Esc en Ctrl+Esc, en blijft het Display-venster boven alle andere vensters. Beheerder kan de kiosk altijd verlaten via rechter-muisklik en admin-PIN. Ctrl+Alt+Del kan in user-mode niet worden geblokkeerd; gebruik Group Policy op de host.",
        AdminCardHelp             = "Help",
        AdminHelpDescription      = "Open de ingebouwde handleiding voor operators. Handig om door te lopen bij eerste ingebruikname of bij het instrueren van nieuwe collega's.",
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

        MassUnmaskConfirmTitle    = "Privacy van alle tegels opheffen?",
        MassUnmaskConfirmBody     = "Met deze actie worden alle privacy-maskers tegelijk verwijderd. Druk op OK om door te gaan, of op Annuleren om elke tegel handmatig te beheren.",
        MassUnmaskAuthTitle       = "Alles vrijgeven",
        PrivacyShowMassUnmaskConfirmLabel = "Bevestiging tonen voordat alles vrijgegeven wordt",
        PrivacyShowMassUnmaskConfirmHelp  = "Toont een dialoog voordat een long-press alle privacy-maskers tegelijk opheft. Aanbevolen op installaties waar de auth-modus uit staat.",
        PrivacyLongPressHelp      = "Houd de muisknop een halve seconde ingedrukt op het scherm om alle tegels in een keer te maskeren of vrij te geven. Single-tap blijft individueel werken.",

        FirstRunLanguageTitle     = "Kies je taal",
        FirstRunLanguageSubtitle  = "Choose your language / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Doorgaan",

        NavAbout                  = "Over",
        AboutPageTitle            = "Over InteractiveMask",
        AboutPageSubtitle         = "Versie, licenties en open-source-componenten.",
        AboutCardProduct          = "Product",
        AboutVersionLabel         = "Versie",
        AboutPublisherLabel       = "Uitgever",
        AboutCopyrightLabel       = "Copyright",
        AboutWebsiteLabel         = "Website",
        AboutDescription          = "InteractiveMask is een Windows-kiosk-applicatie voor live videomonitoring met privacy-bescherming per camerategel. Geschikt voor kantoorruimtes, recepties, hotels, scholen, zorginstellingen en andere omgevingen waar continu zicht en privacy in balans moeten zijn.",
        AboutCardLicensesThird    = "Open-source componenten",
        AboutCardLicensesIntegrations = "Integraties",
        AboutCardBugReport        = "Bug rapporteren",
        AboutBugReportIntro       = "Loop je tegen een probleem aan? Stuur ons een e-mail met een korte beschrijving en, indien mogelijk, een screenshot. We nemen elke melding serieus en proberen zo snel mogelijk te reageren.",
        AboutBugReportButton      = "E-mail openen",
        AboutCardSystemCapabilities = "Systeem-eigenschappen",
        AboutCapsRefreshButton    = "Opnieuw inventariseren",
        AboutCapsProbing          = "Bezig met inventariseren...",
        AboutCapsCpu              = "Processor",
        AboutCapsMemory           = "Geheugen",
        AboutCapsGpu              = "Grafische adapter",
        AboutCapsNpu              = "AI-versneller (NPU)",
        AboutCapsAiTier           = "v2.0 AI-maskering tier",
        AboutCapsNone             = "Geen gedetecteerd",
        AboutCapsIntegrated       = "geïntegreerd",
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
        SetupApply                = "Apply",
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
        CamerasSyncNames          = "Pull names from NVR",
        CamerasSyncNamesProgress  = "Fetching names from NVR...",
        CamerasSyncNamesDoneFormat = "Synchronised: {0} names pulled from the NVR.",
        CamerasSyncNamesNoneApplied = "Sync done: every label is custom and was left as-is.",
        CamerasSyncNamesFailFormat = "Sync failed: {0}",
        CamerasSyncNamesNoCameras = "Add camera rows first.",
        StreamHigh                = "High",
        StreamDefault             = "Default",
        StreamLow                 = "Low",
        CamerasHeaderSlot         = "SLOT",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "CAMERA #",
        CamerasHeaderStream       = "STREAM",
        CamerasHeaderNvrTitle     = "NVR TITLE",
        CamerasHeaderLabel        = "CUSTOM LABEL",

        GridPageTitle             = "Grid",
        GridPageSubtitle          = "How many tiles are shown on screen.",
        GridCardSize              = "Grid size",
        GridTilesCountFormat      = "{0} tiles",
        GridChoice1Caption        = "1 tile (full-screen)",
        GridChoice4Caption        = "4 tiles",
        GridChoice9Caption        = "9 tiles",
        GridChoice16Caption       = "16 tiles",
        GridChoice25Caption       = "25 tiles",

        PrivacyPageTitle          = "Privacy",
        PrivacyPageSubtitle       = "Session-PIN policy, auto-off timer and visual warning.",
        PrivacyCardSessionPin     = "Session PIN for operators",
        PrivacySessionPinHelp     = "When enabled, the system asks for a 4-digit PIN to disable a privacy mask. The first mask of a session sets the PIN; subsequent off-actions in the same session use the same PIN. When off, anyone using the screen can disable a mask immediately.",
        PrivacyCardAutoOff        = "Auto-off timer",
        PrivacyAutoOffMinutesLabel = "Auto-off (minutes, 0 = disabled)",
        PrivacyWarningMinutesLabel = "Warning before auto-off (minutes)",
        PrivacyAutoOffExample     = "Example: with auto-off = 5 and warning = 2, a tile masked at 14:00 shows a pulsing orange border from 14:03; the mask is removed automatically at 14:05.",
        PrivacyCardMassMask       = "Mask / unmask everything (long-press)",
        PrivacyCardMode           = "Default visibility",
        PrivacyModeOversightLabel = "Oversight default",
        PrivacyModeOversightHelp  = "Tiles start visible. An operator taps a tile to apply privacy at the moment it is needed. An auto-off timer makes sure a mask is not left behind by accident.",
        PrivacyModePrivacyLabel   = "Privacy default",
        PrivacyModePrivacyHelp    = "Tiles start blurred. An operator taps to briefly reveal for verification. An auto-rollback timer takes the tile back to blurred. Fits office spaces, receptions, hotels, schools, healthcare facilities, or any other site with stricter privacy defaults.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Require auth on every reveal",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Only active in privacy-default mode. When on, every tap goes through the PIN or AD prompt before the tile is revealed. When off (recommended), a reveal is free because the auto-rollback restores privacy quickly.",
        PrivacyCardTileOverlay    = "Tile overlay",
        PrivacyShowCameraLabel    = "Show custom label",
        PrivacyShowCameraLabelHelp = "Shows the name the administrator typed in the Cameras tab, on the right side of the tile's overlay bar.",
        PrivacyShowNvrTitle       = "Show NVR title",
        PrivacyShowNvrTitleHelp   = "Shows the original camera title as configured on the NVR, on the left side of the tile's overlay bar. Both options can be on at the same time; the NVR title sits on the left, the custom label on the right.",

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
        AdminCardIdentity         = "Operator identity",
        AdminIdentityDescription  = "When enabled, the system prompts for Windows credentials (LogonUser) on every unmask instead of a shared session PIN. The audit log records the username of whoever disabled the mask — useful for accountability and compliance.",
        AdminDomainLabel          = "Default domain (optional)",
        AdminDomainHint           = "Leave empty to use the current machine / domain controller. Operators can also type DOMAIN\\user or user@domain.",
        AdminCardKiosk            = "Kiosk mode",
        AdminKioskDescription     = "When enabled, the system blocks Win key, Alt+Tab, Alt+F4, Alt+Esc and Ctrl+Esc, and keeps the Display window above all others. The administrator can always exit via right-click and admin PIN. Ctrl+Alt+Del cannot be blocked in user mode; use Group Policy on the host.",
        AdminCardHelp             = "Help",
        AdminHelpDescription      = "Open the built-in manual for operators. Useful when first using the system or when training new colleagues.",
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

        MassUnmaskConfirmTitle    = "Lift privacy on all tiles?",
        MassUnmaskConfirmBody     = "This action removes every privacy mask at once. Press OK to continue, or Cancel to manage each tile individually.",
        MassUnmaskAuthTitle       = "Unmask all",
        PrivacyShowMassUnmaskConfirmLabel = "Show a confirmation before unmasking everyone",
        PrivacyShowMassUnmaskConfirmHelp  = "Adds a dialog before a long-press lifts every privacy mask at once. Recommended when authentication is disabled.",
        PrivacyLongPressHelp      = "Hold the mouse button down anywhere on the screen for half a second to mask or unmask every tile at once. Single-tap still toggles tiles individually.",

        FirstRunLanguageTitle     = "Pick your language",
        FirstRunLanguageSubtitle  = "Choose your language / Kies je taal / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Continue",

        NavAbout                  = "About",
        AboutPageTitle            = "About InteractiveMask",
        AboutPageSubtitle         = "Version, licenses and open-source components.",
        AboutCardProduct          = "Product",
        AboutVersionLabel         = "Version",
        AboutPublisherLabel       = "Publisher",
        AboutCopyrightLabel       = "Copyright",
        AboutWebsiteLabel         = "Website",
        AboutDescription          = "InteractiveMask is a Windows kiosk application for live video monitoring with per-tile privacy protection. Built for office spaces, receptions, hotels, schools, healthcare facilities and other environments where continuous oversight and privacy must coexist.",
        AboutCardLicensesThird    = "Open-source components",
        AboutCardLicensesIntegrations = "Integrations",
        AboutCardBugReport        = "Report a bug",
        AboutBugReportIntro       = "Run into a problem? Send us an email with a short description and, if possible, a screenshot. Every report is taken seriously and we try to respond as quickly as we can.",
        AboutBugReportButton      = "Open email client",
        AboutCardSystemCapabilities = "System capabilities",
        AboutCapsRefreshButton    = "Probe again",
        AboutCapsProbing          = "Probing system...",
        AboutCapsCpu              = "Processor",
        AboutCapsMemory           = "Memory",
        AboutCapsGpu              = "Graphics adapter",
        AboutCapsNpu              = "AI accelerator (NPU)",
        AboutCapsAiTier           = "v2.0 AI-masking tier",
        AboutCapsNone             = "None detected",
        AboutCapsIntegrated       = "integrated",
    };

    // ------------------------------------------------------------------
    // German, French, Spanish. Voice: formal "Sie" / "vous" / "usted",
    // matching healthcare's professional register.
    // ------------------------------------------------------------------

    public static readonly StringsTable De = new()
    {
        PrivacyActive             = "Privatsphäre aktiv",
        AutoOff                   = "Auto-Aus",
        PrivacyBadge              = "• PRIVATSPHÄRE",
        EmptyTile                 = "leer",

        StatusConnecting          = "Verbinde...",
        StatusDisconnected        = "Verbindung getrennt",
        StatusVideoLoss           = "Kein Videosignal",
        StatusError               = "Fehler",
        ErrorPrefix               = "Fehler: ",

        PinSetTitle               = "Sitzungs-PIN festlegen",
        PinSetSubtitle            = "Diese PIN wird benötigt, um die Privatsphäre wieder zu deaktivieren.",
        PinVerifyTitle            = "Sitzungs-PIN eingeben",
        PinVerifySubtitle         = "Privatsphäre deaktivieren",
        PinWrong                  = "Ungültige PIN. Bitte erneut versuchen.",
        PinLockedOutFormat        = "Zu viele Versuche. Warten Sie {0} Sekunden.",
        PinHint4Digits            = "4 Ziffern",

        MenuSetup                 = "Einrichtung...",
        MenuExit                  = "Beenden",
        MenuHelp                  = "Hilfe...",
        AdminPinFirstSetupTitle   = "Admin-PIN festlegen",
        AdminPinFirstSetupSub     = "Erststart: Wählen Sie einen 4-stelligen Admin-Code für Beenden und Einrichtung.",
        AdminPinExitTitle         = "Beenden",
        AdminPinExitSub           = "Geben Sie die Admin-PIN ein, um den Kiosk zu beenden.",
        AdminPinSetupOpenTitle    = "Einrichtung",
        AdminPinSetupOpenSub      = "Geben Sie die Admin-PIN ein, um die Einrichtung zu öffnen.",

        SetupTitle                = "InteractiveMask — Einrichtung",
        SetupBrandSub             = "NVR, Kameras und Privatsphäre konfigurieren.",
        SetupCancel               = "Abbrechen",
        SetupApply                = "Übernehmen",
        SetupSave                 = "Speichern",
        SetupConfigLoaded         = "Konfiguration geladen",
        SetupChangesApplied       = "Änderungen übernommen.",
        SetupRestartPrompt        = "Änderungen an NVR-Verbindung, Kamera-Zuweisungen oder Rastergröße erfordern einen Neustart von Display. Jetzt neu starten?",

        NavNvr                    = "NVR",
        NavCameras                = "Kameras",
        NavGrid                   = "Raster",
        NavPrivacy                = "Privatsphäre",
        NavWeb                    = "Web-UI",
        NavAudit                  = "Audit-Protokoll",
        NavAdmin                  = "Administrator",

        NvrPageTitle              = "NVR-Verbindungen",
        NvrPageSubtitle           = "Ein oder mehrere NVRs. Kameras im Tab Kameras können einen beliebigen NVR aus dieser Liste auswählen.",
        NvrCardConnection         = "NVR-Liste",
        NvrFieldIp                = "IP-Adresse",
        NvrFieldPort              = "Port",
        NvrFieldUser              = "Benutzer",
        NvrFieldPassword          = "Passwort",
        NvrPasswordHint           = "Das Passwort wird verschlüsselt über Windows DPAPI (Maschinen-Bereich) gespeichert.",
        NvrAddRow                 = "NVR hinzufügen",
        NvrDeleteRowTooltip       = "NVR löschen",
        NvrEditRowTooltip         = "NVR bearbeiten",
        NvrHeaderName             = "NAME",
        NvrMultiHint              = "Fügen Sie mehrere NVRs hinzu, um ein Raster mit Kameras aus verschiedenen Recordern aufzubauen. Jede Kamera im Tab Kameras wählt ihren NVR über die NVR-Spalte.",
        NvrDialogTitle            = "NVR-Verbindung",
        NvrDialogAddTitle         = "NVR hinzufügen",
        NvrDialogEditTitle        = "NVR bearbeiten",
        NvrDialogSubtitle         = "Direktes LAN, IDIS-Protokoll auf Port 8016 (Standard).",
        NvrDialogEditSubtitle     = "Verbindungsdaten aktualisieren.",
        NvrDialogErrIpEmpty       = "IP-Adresse oder Hostname ist erforderlich.",

        CamerasPageTitle          = "Kameras",
        CamerasPageSubtitle       = "Welche Kameras werden in welchem Slot des Rasters angezeigt.",
        CamerasCardBindings       = "Slot-Zuweisungen",
        CamerasAddRow             = "Zeile hinzufügen",
        CamerasHelpText           = "Slot = Position im Raster (1..N). Wählen Sie bei NVR, von welchem Recorder diese Kamera kommt. Kamera # = Kamerakanal auf diesem NVR (beginnt bei 1). Wählen Sie Normal beim Stream für die Live-Mehrkameraansicht; Hoch erfordert mehr Bandbreite.",
        CamerasDeleteRowTooltip   = "Zeile löschen",
        CamerasReorderTooltip     = "Ziehen, um die Reihenfolge der Kacheln zu ändern",
        CamerasSyncNames          = "Namen vom NVR abrufen",
        CamerasSyncNamesProgress  = "Namen werden vom NVR abgerufen...",
        CamerasSyncNamesDoneFormat = "Synchronisiert: {0} Namen vom NVR aktualisiert.",
        CamerasSyncNamesNoneApplied = "Synchronisierung abgeschlossen: alle Bezeichnungen sind benutzerdefiniert und wurden unverändert gelassen.",
        CamerasSyncNamesFailFormat = "Synchronisierung fehlgeschlagen: {0}",
        CamerasSyncNamesNoCameras = "Fügen Sie zuerst Kamera-Zeilen hinzu.",
        StreamHigh                = "Hoch",
        StreamDefault             = "Normal",
        StreamLow                 = "Niedrig",
        CamerasHeaderSlot         = "SLOT",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "KAMERA #",
        CamerasHeaderStream       = "STREAM",
        CamerasHeaderNvrTitle     = "NVR-TITEL",
        CamerasHeaderLabel        = "EIGENE BEZEICHNUNG",

        GridPageTitle             = "Raster",
        GridPageSubtitle          = "Wie viele Kacheln werden auf dem Bildschirm angezeigt.",
        GridCardSize              = "Rastergröße",
        GridTilesCountFormat      = "{0} Kacheln",
        GridChoice1Caption        = "1 Kachel (Vollbild)",
        GridChoice4Caption        = "4 Kacheln",
        GridChoice9Caption        = "9 Kacheln",
        GridChoice16Caption       = "16 Kacheln",
        GridChoice25Caption       = "25 Kacheln",

        PrivacyPageTitle          = "Privatsphäre",
        PrivacyPageSubtitle       = "Sitzungs-PIN-Richtlinie, Auto-Aus-Timer und visuelle Warnung.",
        PrivacyCardSessionPin     = "Sitzungs-PIN für Bediener",
        PrivacySessionPinHelp     = "Wenn aktiviert, fragt das System nach einer 4-stelligen PIN, um eine Privatsphären-Maske zu deaktivieren. Bei der ersten Maske einer Sitzung wird die PIN festgelegt; alle nachfolgenden Deaktivierungen in derselben Sitzung verwenden dieselbe PIN. Wenn deaktiviert, kann jeder, der den Bildschirm bedient, eine Maske sofort deaktivieren.",
        PrivacyCardAutoOff        = "Auto-Aus-Timer",
        PrivacyAutoOffMinutesLabel = "Auto-Aus (Minuten, 0 = deaktiviert)",
        PrivacyWarningMinutesLabel = "Warnung vor Auto-Aus (Minuten)",
        PrivacyAutoOffExample     = "Beispiel: Bei Auto-Aus = 5 und Warnung = 2 erscheint auf einer um 14:00 maskierten Kachel ab 14:03 ein pulsierender orangefarbener Rand; um 14:05 wird die Maske automatisch entfernt.",
        PrivacyCardMassMask       = "Alles maskieren / freigeben (Long-Press)",
        PrivacyCardMode           = "Standardsichtbarkeit",
        PrivacyModeOversightLabel = "Standardansicht",
        PrivacyModeOversightHelp  = "Die Kacheln starten sichtbar. Der Bediener tippt auf eine Kachel, um die Privatsphäre in dem Moment zu aktivieren, in dem es notwendig ist. Ein Auto-Aus-Timer sorgt dafür, dass eine Maske nicht versehentlich aktiv bleibt.",
        PrivacyModePrivacyLabel   = "Standard-Privat",
        PrivacyModePrivacyHelp    = "Die Kacheln starten unscharf. Der Bediener tippt, um sie zur Verifizierung kurz aufzudecken. Ein Auto-Rollback-Timer bringt die Kachel automatisch wieder in den unscharfen Zustand. Geeignet für Büroräume, Empfangsbereiche, Hotels, Schulen, Pflegeeinrichtungen oder jeden anderen Standort mit strengeren Datenschutzvorgaben.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Authentifizierung bei jeder Aufdeckung verlangen",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Nur im Standard-Privat-Modus aktiv. Wenn aktiviert, durchläuft jeder Tipp die PIN- oder AD-Abfrage, bevor die Kachel aufgedeckt wird. Wenn deaktiviert (empfohlen), ist eine Aufdeckung frei, weil das Auto-Rollback die Privatsphäre schnell wiederherstellt.",
        PrivacyCardTileOverlay    = "Kachel-Overlay",
        PrivacyShowCameraLabel    = "Benutzerdefinierten Namen anzeigen",
        PrivacyShowCameraLabelHelp = "Zeigt den vom Administrator im Tab Kameras eingegebenen Namen rechts in der Overlay-Leiste der Kachel an.",
        PrivacyShowNvrTitle       = "NVR-Titel anzeigen",
        PrivacyShowNvrTitleHelp   = "Zeigt den ursprünglichen Kameratitel, wie er auf dem NVR konfiguriert ist, links in der Overlay-Leiste der Kachel an. Beide Optionen können gleichzeitig aktiv sein; der NVR-Titel steht dann links, die benutzerdefinierte Bezeichnung rechts.",

        WebPageTitle              = "Web-UI",
        WebPageSubtitle           = "Ports, Zertifikat und Netzwerkerreichbarkeit der Browser-Bedienung.",
        WebCardPorts              = "Ports",
        WebHttpPortLabel          = "HTTP-Port",
        WebHttpsPortLabel         = "HTTPS-Port (leer = aus)",
        WebCardLan                = "Erreichbarkeit im LAN",
        WebLanHelp                = "Wenn deaktiviert, hört der WebHost nur auf localhost (nur von dieser Maschine erreichbar). Wenn aktiviert, auch auf der LAN-IP-Adresse, sodass Kollegen-Workstations oder ein Tablet aus der Ferne bedienen können.",
        WebCardCert               = "HTTPS-Zertifikat",
        WebCertIntro              = "Pfad zu einer PFX-Datei. Das Passwort wird verschlüsselt gespeichert (DPAPI). Oder klicken Sie auf 'Selbstsigniert generieren', um ein neues Zertifikat in ProgramData zu erstellen.",
        WebCertPathLabel          = "PFX-Datei",
        WebCertBrowse             = "Durchsuchen...",
        WebCertPasswordLabel      = "Passwort",
        WebCertGenerate           = "Selbstsigniertes Zertifikat generieren",
        WebCertRestartHint        = "Änderungen an Ports oder Zertifikat werden erst nach einem Neustart des WebHost-Dienstes wirksam.",

        WebAccessCardTitle        = "Zugang zur Web-Oberfläche",
        WebAccessCardDescription  = "Legen Sie fest, ob die Web-Oberfläche offen zugänglich ist oder ob zuerst eine Anmeldung erforderlich ist. Dies ist unabhängig von der PIN/AD-Richtlinie pro Kachel zum Deaktivieren einer Privatsphären-Maske.",
        WebAccessMode             = "Zugangsmodus",
        WebAccessModeOff          = "Offen (keine Anmeldung)",
        WebAccessModePin          = "Gemeinsame Zugangs-PIN",
        WebAccessModeAd           = "Windows-Anmeldung",
        WebAccessPin              = "Zugangs-PIN",
        WebAccessDomain           = "Standarddomäne (optional)",

        AuditPageTitle            = "Audit-Protokoll",
        AuditPageSubtitle         = "Vollständige Historie der Maskenaktionen, PIN-Ereignisse und NVR-Verbindungen.",
        AuditCardRecent           = "Aktuelle Ereignisse",
        AuditRefresh              = "Aktualisieren",
        AuditExportCsv            = "Als CSV exportieren...",
        AuditHeaderTimestamp      = "ZEITSTEMPEL",
        AuditHeaderType           = "TYP",
        AuditHeaderSlot           = "SLOT",
        AuditHeaderLabel          = "BESCHRIFTUNG",
        AuditHeaderSource         = "QUELLE",
        AuditHeaderDetail         = "DETAILS",

        AuditCardForwarding       = "Syslog-Weiterleitung",
        AuditSyslogEnabled        = "Audit-Ereignisse an externen SIEM-/Syslog-Server weiterleiten",
        AuditSyslogHost           = "Host",
        AuditSyslogPort           = "Port",
        AuditSyslogProtocol       = "Protokoll",
        AuditSyslogAppName        = "App-Name (RFC 5424)",
        AuditSyslogFacility       = "Facility (0-23)",
        AuditSyslogTest           = "Testnachricht senden",
        AuditSyslogTesting        = "Wird gesendet...",
        AuditSyslogTestOk         = "Testnachricht gesendet.",
        AuditSyslogTestFailFormat = "Senden fehlgeschlagen: {0}",
        AuditSyslogTestNoHost     = "Geben Sie zuerst einen Host ein.",

        AdminPageTitle            = "Administrator",
        AdminPageSubtitle         = "Zugang für Beenden und Einrichtung sowie Sprachauswahl.",
        AdminCardPin              = "Admin-PIN",
        AdminPinDescription       = "Erforderlich beim Beenden des Kiosks und beim Öffnen der Einrichtung. Vier Ziffern.",
        AdminPinChangeButton      = "Neue Admin-PIN festlegen...",
        AdminCardLanguage         = "Sprache der Benutzeroberfläche",
        AdminLangNl               = "Niederländisch",
        AdminLangEn               = "Englisch",
        AdminLanguageHint         = "Änderungen werden sofort auf alle angezeigten Texte angewendet.",
        AdminCardIdentity         = "Identität der Bediener",
        AdminIdentityDescription  = "Wenn aktiviert, fordert das System bei jeder Aufdeckung eine Windows-Anmeldung (LogonUser) anstelle einer gemeinsamen Sitzungs-PIN. Das Audit-Protokoll erfasst den Benutzernamen der Person, die die Maske deaktiviert hat — sinnvoll für Nachvollziehbarkeit und Compliance.",
        AdminDomainLabel          = "Standarddomäne (optional)",
        AdminDomainHint           = "Leer lassen, um die aktuelle Maschine / den Domain Controller zu verwenden. Bediener können auch DOMAIN\\Benutzer oder benutzer@domain eingeben.",
        AdminCardKiosk            = "Kiosk-Modus",
        AdminKioskDescription     = "Wenn aktiviert, blockiert das System Win-Taste, Alt+Tab, Alt+F4, Alt+Esc und Strg+Esc und hält das Display-Fenster über allen anderen Fenstern. Der Administrator kann den Kiosk jederzeit über Rechtsklick und Admin-PIN verlassen. Strg+Alt+Entf kann im Benutzermodus nicht blockiert werden; verwenden Sie Gruppenrichtlinien auf dem Host.",
        AdminCardHelp             = "Hilfe",
        AdminHelpDescription      = "Öffnet das integrierte Handbuch für Bediener. Hilfreich beim ersten Einsatz oder bei der Schulung neuer Kollegen.",
        AdminHelpOpen             = "Handbuch öffnen",

        SetupErrPort              = "Port muss eine Zahl sein.",
        SetupErrAutoMinutes       = "Auto-Aus-Timer muss 0 oder höher sein.",
        SetupErrWarningMinutes    = "Warnminuten müssen 0 oder höher sein.",
        SetupErrHttpPort          = "HTTP-Port muss eine positive Zahl sein.",
        SetupErrHttpsPort         = "HTTPS-Port muss eine positive Zahl sein (oder leer für aus).",
        SetupErrDuplicateSlotFormat   = "Slot {0} wird mehr als einmal verwendet. Jede Rasterposition kann nur eine Kamera enthalten.",
        SetupErrDuplicateCameraFormat = "Kamera {0} erscheint mehr als einmal. Jede Kamera kann nur an einer Position platziert werden.",
        SetupErrDuplicateNvrFormat    = "NVR-ID {0} erscheint mehr als einmal. Jeder NVR muss eine eindeutige ID haben.",
        SetupErrCameraOrphanFormat    = "Slot {0} verweist auf einen NVR, der nicht mehr in der Liste ist. Wählen Sie einen gültigen NVR.",
        SetupErrNvrInUseFormat        = "NVR \"{0}\" wird von einer oder mehreren Kameras verwendet. Verschieben Sie diese Kameras zu einem anderen NVR, bevor Sie diesen löschen.",
        SetupErrNoNvr                 = "Fügen Sie mindestens einen NVR hinzu, bevor Sie speichern.",
        SetupErrSaveFailedFormat  = "Speichern fehlgeschlagen: {0}",
        SetupCertCreatedFormat    = "Selbstsigniertes Zertifikat erstellt: {0}",
        SetupCertCreateFailedFormat = "Zertifikat konnte nicht erstellt werden: {0}",
        SetupAdminPinUpdated      = "Admin-PIN aktualisiert.",
        SetupExportedFormat       = "Exportiert: {0}",
        SetupExportFailedFormat   = "Export fehlgeschlagen: {0}",
        SetupBrowseCertTitle      = "PFX-Datei auswählen",
        SetupBrowseCertFilter     = "Zertifikat (*.pfx;*.p12)|*.pfx;*.p12|Alle Dateien (*.*)|*.*",
        SetupAuditExportTitle     = "Audit-Protokoll exportieren",
        SetupAuditExportFilter    = "CSV (*.csv)|*.csv",
        SetupAdminPinNewTitle     = "Neue Admin-PIN",
        SetupAdminPinNewSub       = "Legen Sie einen neuen 4-stelligen Admin-Code fest.",
        SetupAdLoginSubtitle      = "Melden Sie sich mit Ihrem Windows-Konto an.",
        SetupAdRemoveTitle        = "Privatsphäre deaktivieren",

        MainNoConfigLine          = "NVR-Konfiguration fehlt. Rechtsklick für Einrichtung / Beenden.",

        ConnectionLost            = "Verbindung zum NVR verloren",
        ConnectionLostFormat      = "{0} — Erneuter Verbindungsversuch in {1} s...",
        ConnectionAttempting      = "Verbindung zum NVR...",

        MassUnmaskConfirmTitle    = "Privatsphäre aller Kacheln aufheben?",
        MassUnmaskConfirmBody     = "Diese Aktion entfernt alle Privatsphären-Masken auf einmal. Drücken Sie OK, um fortzufahren, oder Abbrechen, um jede Kachel einzeln zu verwalten.",
        MassUnmaskAuthTitle       = "Alles freigeben",
        PrivacyShowMassUnmaskConfirmLabel = "Bestätigung anzeigen, bevor alles freigegeben wird",
        PrivacyShowMassUnmaskConfirmHelp  = "Zeigt einen Dialog, bevor ein Long-Press alle Privatsphären-Masken auf einmal aufhebt. Empfohlen für Installationen, bei denen die Authentifizierung deaktiviert ist.",
        PrivacyLongPressHelp      = "Halten Sie die Maustaste eine halbe Sekunde lang an einer beliebigen Stelle gedrückt, um alle Kacheln auf einmal zu maskieren oder freizugeben. Single-Tap funktioniert weiterhin pro Kachel.",

        FirstRunLanguageTitle     = "Wählen Sie Ihre Sprache",
        FirstRunLanguageSubtitle  = "Choose your language / Kies je taal / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Weiter",

        NavAbout                  = "Über",
        AboutPageTitle            = "Über InteractiveMask",
        AboutPageSubtitle         = "Version, Lizenzen und Open-Source-Komponenten.",
        AboutCardProduct          = "Produkt",
        AboutVersionLabel         = "Version",
        AboutPublisherLabel       = "Herausgeber",
        AboutCopyrightLabel       = "Copyright",
        AboutWebsiteLabel         = "Webseite",
        AboutDescription          = "InteractiveMask ist eine Windows-Kiosk-Anwendung für die Live-Videoüberwachung mit Datenschutz pro Kamerakachel. Geeignet für Büroräume, Empfangsbereiche, Hotels, Schulen, Pflegeeinrichtungen und andere Umgebungen, in denen kontinuierliche Übersicht und Datenschutz im Gleichgewicht stehen müssen.",
        AboutCardLicensesThird    = "Open-Source-Komponenten",
        AboutCardLicensesIntegrations = "Integrationen",
        AboutCardBugReport        = "Fehler melden",
        AboutBugReportIntro       = "Probleme aufgetreten? Senden Sie uns eine E-Mail mit einer kurzen Beschreibung und, wenn möglich, einem Screenshot. Jede Meldung wird ernst genommen und wir versuchen, so schnell wie möglich zu antworten.",
        AboutBugReportButton      = "E-Mail öffnen",
        AboutCardSystemCapabilities = "Systemeigenschaften",
        AboutCapsRefreshButton    = "Erneut prüfen",
        AboutCapsProbing          = "System wird geprüft...",
        AboutCapsCpu              = "Prozessor",
        AboutCapsMemory           = "Arbeitsspeicher",
        AboutCapsGpu              = "Grafikadapter",
        AboutCapsNpu              = "KI-Beschleuniger (NPU)",
        AboutCapsAiTier           = "v2.0 KI-Maskierungs-Tier",
        AboutCapsNone             = "Nicht erkannt",
        AboutCapsIntegrated       = "integriert",
    };

    public static readonly StringsTable Fr = new()
    {
        PrivacyActive             = "Confidentialité active",
        AutoOff                   = "auto-désact.",
        PrivacyBadge              = "• CONFIDENTIALITÉ",
        EmptyTile                 = "vide",

        StatusConnecting          = "Connexion...",
        StatusDisconnected        = "Connexion perdue",
        StatusVideoLoss           = "Aucun signal vidéo",
        StatusError               = "Erreur",
        ErrorPrefix               = "Erreur : ",

        PinSetTitle               = "Définir le PIN de session",
        PinSetSubtitle            = "Ce PIN est nécessaire pour désactiver la confidentialité.",
        PinVerifyTitle            = "Saisir le PIN de session",
        PinVerifySubtitle         = "Désactiver la confidentialité",
        PinWrong                  = "PIN incorrect. Veuillez réessayer.",
        PinLockedOutFormat        = "Trop de tentatives. Attendez {0} secondes.",
        PinHint4Digits            = "4 chiffres",

        MenuSetup                 = "Configuration...",
        MenuExit                  = "Quitter",
        MenuHelp                  = "Aide...",
        AdminPinFirstSetupTitle   = "Définir le PIN administrateur",
        AdminPinFirstSetupSub     = "Premier démarrage : choisissez un code administrateur à 4 chiffres pour quitter et la configuration.",
        AdminPinExitTitle         = "Quitter",
        AdminPinExitSub           = "Saisir le PIN administrateur pour quitter le kiosque.",
        AdminPinSetupOpenTitle    = "Configuration",
        AdminPinSetupOpenSub      = "Saisir le PIN administrateur pour ouvrir la configuration.",

        SetupTitle                = "InteractiveMask — Configuration",
        SetupBrandSub             = "Configurer NVR, caméras et confidentialité.",
        SetupCancel               = "Annuler",
        SetupApply                = "Appliquer",
        SetupSave                 = "Enregistrer",
        SetupConfigLoaded         = "Configuration chargée",
        SetupChangesApplied       = "Modifications appliquées.",
        SetupRestartPrompt        = "Les modifications de la connexion NVR, des affectations de caméras ou de la taille de la grille nécessitent un redémarrage de Display. Redémarrer maintenant ?",

        NavNvr                    = "NVR",
        NavCameras                = "Caméras",
        NavGrid                   = "Grille",
        NavPrivacy                = "Confidentialité",
        NavWeb                    = "Interface Web",
        NavAudit                  = "Journal d'audit",
        NavAdmin                  = "Administrateur",

        NvrPageTitle              = "Connexions NVR",
        NvrPageSubtitle           = "Un ou plusieurs NVR. Les caméras dans l'onglet Caméras peuvent choisir n'importe quel NVR de cette liste.",
        NvrCardConnection         = "Liste des NVR",
        NvrFieldIp                = "Adresse IP",
        NvrFieldPort              = "Port",
        NvrFieldUser              = "Utilisateur",
        NvrFieldPassword          = "Mot de passe",
        NvrPasswordHint           = "Le mot de passe est stocké chiffré via Windows DPAPI (portée machine).",
        NvrAddRow                 = "Ajouter un NVR",
        NvrDeleteRowTooltip       = "Supprimer le NVR",
        NvrEditRowTooltip         = "Modifier le NVR",
        NvrHeaderName             = "NOM",
        NvrMultiHint              = "Ajoutez plusieurs NVR pour construire une grille avec des caméras provenant de différents enregistreurs. Chaque caméra dans l'onglet Caméras choisit son NVR via la colonne NVR.",
        NvrDialogTitle            = "Connexion NVR",
        NvrDialogAddTitle         = "Ajouter un NVR",
        NvrDialogEditTitle        = "Modifier le NVR",
        NvrDialogSubtitle         = "LAN direct, protocole IDIS sur le port 8016 (par défaut).",
        NvrDialogEditSubtitle     = "Mettez à jour les informations de connexion.",
        NvrDialogErrIpEmpty       = "L'adresse IP ou le nom d'hôte est obligatoire.",

        CamerasPageTitle          = "Caméras",
        CamerasPageSubtitle       = "Quelles caméras sont affichées dans quel emplacement de la grille.",
        CamerasCardBindings       = "Affectations d'emplacement",
        CamerasAddRow             = "Ajouter une ligne",
        CamerasHelpText           = "Emplacement = position dans la grille (1..N). Choisissez dans NVR de quel enregistreur provient cette caméra. Caméra # = canal de caméra sur ce NVR (commence à 1). Choisissez Normal pour Stream pour la vue multi-caméras en direct ; Élevé consomme plus de bande passante.",
        CamerasDeleteRowTooltip   = "Supprimer la ligne",
        CamerasReorderTooltip     = "Glisser pour réorganiser l'ordre des tuiles",
        CamerasSyncNames          = "Récupérer les noms depuis le NVR",
        CamerasSyncNamesProgress  = "Récupération des noms depuis le NVR...",
        CamerasSyncNamesDoneFormat = "Synchronisé : {0} noms mis à jour depuis le NVR.",
        CamerasSyncNamesNoneApplied = "Synchronisation terminée : toutes les étiquettes sont personnalisées et sont restées inchangées.",
        CamerasSyncNamesFailFormat = "Échec de la synchronisation : {0}",
        CamerasSyncNamesNoCameras = "Ajoutez d'abord des lignes de caméra.",
        StreamHigh                = "Élevé",
        StreamDefault             = "Normal",
        StreamLow                 = "Bas",
        CamerasHeaderSlot         = "EMPLACEMENT",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "CAMÉRA #",
        CamerasHeaderStream       = "FLUX",
        CamerasHeaderNvrTitle     = "TITRE NVR",
        CamerasHeaderLabel        = "ÉTIQUETTE PERSONNALISÉE",

        GridPageTitle             = "Grille",
        GridPageSubtitle          = "Combien de tuiles sont affichées à l'écran.",
        GridCardSize              = "Taille de la grille",
        GridTilesCountFormat      = "{0} tuiles",
        GridChoice1Caption        = "1 tuile (plein écran)",
        GridChoice4Caption        = "4 tuiles",
        GridChoice9Caption        = "9 tuiles",
        GridChoice16Caption       = "16 tuiles",
        GridChoice25Caption       = "25 tuiles",

        PrivacyPageTitle          = "Confidentialité",
        PrivacyPageSubtitle       = "Politique du PIN de session, minuteur d'auto-désactivation et avertissement visuel.",
        PrivacyCardSessionPin     = "PIN de session pour les opérateurs",
        PrivacySessionPinHelp     = "Lorsqu'il est activé, le système demande un PIN à 4 chiffres pour désactiver un masque de confidentialité. Le premier masque d'une session définit le PIN ; toutes les désactivations suivantes dans la même session utilisent le même PIN. Lorsqu'il est désactivé, toute personne utilisant l'écran peut désactiver un masque immédiatement.",
        PrivacyCardAutoOff        = "Minuteur d'auto-désactivation",
        PrivacyAutoOffMinutesLabel = "Auto-désactivation (minutes, 0 = désactivé)",
        PrivacyWarningMinutesLabel = "Avertissement avant auto-désactivation (minutes)",
        PrivacyAutoOffExample     = "Exemple : avec auto-désactivation = 5 et avertissement = 2, une tuile masquée à 14h00 affiche une bordure orange clignotante à partir de 14h03 ; le masque est supprimé automatiquement à 14h05.",
        PrivacyCardMassMask       = "Tout masquer / libérer (appui long)",
        PrivacyCardMode           = "Visibilité par défaut",
        PrivacyModeOversightLabel = "Vue par défaut",
        PrivacyModeOversightHelp  = "Les tuiles démarrent visibles. L'opérateur tape sur une tuile pour appliquer la confidentialité au moment où cela est nécessaire. Un minuteur d'auto-désactivation garantit qu'un masque ne reste pas en place par accident.",
        PrivacyModePrivacyLabel   = "Confidentialité par défaut",
        PrivacyModePrivacyHelp    = "Les tuiles démarrent floutées. L'opérateur tape pour révéler brièvement à des fins de vérification. Un minuteur d'auto-rollback ramène la tuile au flou. Convient aux espaces de bureau, aux réceptions, aux hôtels, aux écoles, aux établissements de soins ou à tout autre site avec des paramètres de confidentialité plus stricts.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Demander une authentification à chaque révélation",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Actif uniquement en mode confidentialité par défaut. Lorsqu'il est activé, chaque tap passe par l'invite PIN ou AD avant que la tuile ne soit révélée. Lorsqu'il est désactivé (recommandé), une révélation est libre car l'auto-rollback rétablit rapidement la confidentialité.",
        PrivacyCardTileOverlay    = "Superposition de tuile",
        PrivacyShowCameraLabel    = "Afficher le nom personnalisé",
        PrivacyShowCameraLabelHelp = "Affiche le nom saisi par l'administrateur dans l'onglet Caméras, sur la droite de la barre d'overlay de la tuile.",
        PrivacyShowNvrTitle       = "Afficher le titre NVR",
        PrivacyShowNvrTitleHelp   = "Affiche le titre original de la caméra tel qu'il est configuré sur le NVR, sur la gauche de la barre d'overlay de la tuile. Les deux options peuvent être activées simultanément ; le titre NVR à gauche, l'étiquette personnalisée à droite.",

        WebPageTitle              = "Interface Web",
        WebPageSubtitle           = "Ports, certificat et accessibilité réseau pour le contrôle par navigateur.",
        WebCardPorts              = "Ports",
        WebHttpPortLabel          = "Port HTTP",
        WebHttpsPortLabel         = "Port HTTPS (vide = désactivé)",
        WebCardLan                = "Accessibilité sur le LAN",
        WebLanHelp                = "Lorsqu'il est désactivé, le WebHost n'écoute que sur localhost (uniquement cette machine). Lorsqu'il est activé, il écoute également sur l'adresse IP LAN afin que les postes de travail des collègues ou une tablette puissent contrôler à distance.",
        WebCardCert               = "Certificat HTTPS",
        WebCertIntro              = "Chemin vers un fichier PFX. Le mot de passe est stocké chiffré (DPAPI). Ou cliquez sur 'Générer auto-signé' pour créer un nouveau certificat dans ProgramData.",
        WebCertPathLabel          = "Fichier PFX",
        WebCertBrowse             = "Parcourir...",
        WebCertPasswordLabel      = "Mot de passe",
        WebCertGenerate           = "Générer un certificat auto-signé",
        WebCertRestartHint        = "Les modifications des ports ou du certificat ne prennent effet qu'après un redémarrage du service WebHost.",

        WebAccessCardTitle        = "Accès à l'interface Web",
        WebAccessCardDescription  = "Choisissez si l'interface Web est librement accessible ou si une connexion est requise. Cela est indépendant de la politique PIN/AD par tuile utilisée pour désactiver un masque de confidentialité.",
        WebAccessMode             = "Mode d'accès",
        WebAccessModeOff          = "Ouvert (pas de connexion)",
        WebAccessModePin          = "PIN d'accès partagé",
        WebAccessModeAd           = "Connexion Windows",
        WebAccessPin              = "PIN d'accès",
        WebAccessDomain           = "Domaine par défaut (facultatif)",

        AuditPageTitle            = "Journal d'audit",
        AuditPageSubtitle         = "Historique complet des actions de masque, des événements PIN et des connexions NVR.",
        AuditCardRecent           = "Événements récents",
        AuditRefresh              = "Actualiser",
        AuditExportCsv            = "Exporter en CSV...",
        AuditHeaderTimestamp      = "HORODATAGE",
        AuditHeaderType           = "TYPE",
        AuditHeaderSlot           = "EMPLACEMENT",
        AuditHeaderLabel          = "ÉTIQUETTE",
        AuditHeaderSource         = "SOURCE",
        AuditHeaderDetail         = "DÉTAIL",

        AuditCardForwarding       = "Transfert Syslog",
        AuditSyslogEnabled        = "Transférer les événements d'audit vers un serveur SIEM/syslog externe",
        AuditSyslogHost           = "Hôte",
        AuditSyslogPort           = "Port",
        AuditSyslogProtocol       = "Protocole",
        AuditSyslogAppName        = "Nom d'application (RFC 5424)",
        AuditSyslogFacility       = "Facility (0-23)",
        AuditSyslogTest           = "Envoyer un message de test",
        AuditSyslogTesting        = "Envoi en cours...",
        AuditSyslogTestOk         = "Message de test envoyé.",
        AuditSyslogTestFailFormat = "Échec de l'envoi : {0}",
        AuditSyslogTestNoHost     = "Saisissez d'abord un hôte.",

        AdminPageTitle            = "Administrateur",
        AdminPageSubtitle         = "Accès pour quitter et configuration, et préférence de langue.",
        AdminCardPin              = "PIN administrateur",
        AdminPinDescription       = "Requis lors de la sortie du kiosque et de l'ouverture de la configuration. Quatre chiffres.",
        AdminPinChangeButton      = "Définir un nouveau PIN administrateur...",
        AdminCardLanguage         = "Langue de l'interface utilisateur",
        AdminLangNl               = "Néerlandais",
        AdminLangEn               = "Anglais",
        AdminLanguageHint         = "Les modifications sont appliquées immédiatement à tous les textes affichés.",
        AdminCardIdentity         = "Identité des opérateurs",
        AdminIdentityDescription  = "Lorsqu'il est activé, le système demande des informations d'identification Windows (LogonUser) à chaque révélation au lieu d'un PIN de session partagé. Le journal d'audit enregistre le nom d'utilisateur de la personne qui a désactivé le masque — utile pour la responsabilité et la conformité.",
        AdminDomainLabel          = "Domaine par défaut (facultatif)",
        AdminDomainHint           = "Laissez vide pour utiliser la machine actuelle / le contrôleur de domaine. Les opérateurs peuvent également saisir DOMAINE\\utilisateur ou utilisateur@domaine.",
        AdminCardKiosk            = "Mode kiosque",
        AdminKioskDescription     = "Lorsqu'il est activé, le système bloque la touche Win, Alt+Tab, Alt+F4, Alt+Échap et Ctrl+Échap, et maintient la fenêtre Display au-dessus de toutes les autres. L'administrateur peut toujours quitter le kiosque via clic droit et PIN administrateur. Ctrl+Alt+Suppr ne peut pas être bloqué en mode utilisateur ; utilisez la stratégie de groupe sur l'hôte.",
        AdminCardHelp             = "Aide",
        AdminHelpDescription      = "Ouvrez le manuel intégré pour les opérateurs. Utile lors de la première utilisation ou pour former de nouveaux collègues.",
        AdminHelpOpen             = "Ouvrir le manuel",

        SetupErrPort              = "Le port doit être un nombre.",
        SetupErrAutoMinutes       = "Le minuteur d'auto-désactivation doit être 0 ou plus.",
        SetupErrWarningMinutes    = "Les minutes d'avertissement doivent être 0 ou plus.",
        SetupErrHttpPort          = "Le port HTTP doit être un nombre positif.",
        SetupErrHttpsPort         = "Le port HTTPS doit être un nombre positif (ou vide pour désactivé).",
        SetupErrDuplicateSlotFormat   = "L'emplacement {0} est utilisé plus d'une fois. Chaque position de la grille ne peut contenir qu'une seule caméra.",
        SetupErrDuplicateCameraFormat = "La caméra {0} apparaît plus d'une fois. Chaque caméra ne peut être placée que sur une seule position.",
        SetupErrDuplicateNvrFormat    = "L'ID NVR {0} apparaît plus d'une fois. Chaque NVR doit avoir un ID unique.",
        SetupErrCameraOrphanFormat    = "L'emplacement {0} fait référence à un NVR qui n'est plus dans la liste. Sélectionnez un NVR valide.",
        SetupErrNvrInUseFormat        = "Le NVR \"{0}\" est utilisé par une ou plusieurs caméras. Déplacez ces caméras vers un autre NVR avant de supprimer celui-ci.",
        SetupErrNoNvr                 = "Ajoutez au moins un NVR avant d'enregistrer.",
        SetupErrSaveFailedFormat  = "Échec de l'enregistrement : {0}",
        SetupCertCreatedFormat    = "Certificat auto-signé créé : {0}",
        SetupCertCreateFailedFormat = "Impossible de créer le certificat : {0}",
        SetupAdminPinUpdated      = "PIN administrateur mis à jour.",
        SetupExportedFormat       = "Exporté : {0}",
        SetupExportFailedFormat   = "Échec de l'exportation : {0}",
        SetupBrowseCertTitle      = "Sélectionner un fichier PFX",
        SetupBrowseCertFilter     = "Certificat (*.pfx;*.p12)|*.pfx;*.p12|Tous les fichiers (*.*)|*.*",
        SetupAuditExportTitle     = "Exporter le journal d'audit",
        SetupAuditExportFilter    = "CSV (*.csv)|*.csv",
        SetupAdminPinNewTitle     = "Nouveau PIN administrateur",
        SetupAdminPinNewSub       = "Définissez un nouveau code administrateur à 4 chiffres.",
        SetupAdLoginSubtitle      = "Connectez-vous avec votre compte Windows.",
        SetupAdRemoveTitle        = "Désactiver la confidentialité",

        MainNoConfigLine          = "Configuration NVR manquante. Clic droit pour Configuration / Quitter.",

        ConnectionLost            = "Connexion au NVR perdue",
        ConnectionLostFormat      = "{0} — nouvelle tentative dans {1} s...",
        ConnectionAttempting      = "Connexion au NVR...",

        MassUnmaskConfirmTitle    = "Lever la confidentialité de toutes les tuiles ?",
        MassUnmaskConfirmBody     = "Cette action supprime tous les masques de confidentialité en une fois. Appuyez sur OK pour continuer, ou Annuler pour gérer chaque tuile individuellement.",
        MassUnmaskAuthTitle       = "Tout libérer",
        PrivacyShowMassUnmaskConfirmLabel = "Afficher une confirmation avant de tout libérer",
        PrivacyShowMassUnmaskConfirmHelp  = "Affiche un dialogue avant qu'un appui long ne lève tous les masques de confidentialité en une fois. Recommandé pour les installations où l'authentification est désactivée.",
        PrivacyLongPressHelp      = "Maintenez le bouton de la souris enfoncé pendant une demi-seconde n'importe où à l'écran pour masquer ou démasquer toutes les tuiles à la fois. Le tap simple continue de fonctionner par tuile.",

        FirstRunLanguageTitle     = "Choisissez votre langue",
        FirstRunLanguageSubtitle  = "Choose your language / Kies je taal / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Continuer",

        NavAbout                  = "À propos",
        AboutPageTitle            = "À propos d'InteractiveMask",
        AboutPageSubtitle         = "Version, licences et composants open source.",
        AboutCardProduct          = "Produit",
        AboutVersionLabel         = "Version",
        AboutPublisherLabel       = "Éditeur",
        AboutCopyrightLabel       = "Copyright",
        AboutWebsiteLabel         = "Site web",
        AboutDescription          = "InteractiveMask est une application kiosque Windows pour la surveillance vidéo en direct avec protection de la confidentialité par tuile. Conçue pour les espaces de bureau, les réceptions, les hôtels, les écoles, les établissements de soins et d'autres environnements où la surveillance continue et la confidentialité doivent coexister.",
        AboutCardLicensesThird    = "Composants open source",
        AboutCardLicensesIntegrations = "Intégrations",
        AboutCardBugReport        = "Signaler un bogue",
        AboutBugReportIntro       = "Vous rencontrez un problème ? Envoyez-nous un e-mail avec une brève description et, si possible, une capture d'écran. Chaque signalement est pris au sérieux et nous essayons de répondre le plus rapidement possible.",
        AboutBugReportButton      = "Ouvrir le client de messagerie",
        AboutCardSystemCapabilities = "Caractéristiques système",
        AboutCapsRefreshButton    = "Sonder à nouveau",
        AboutCapsProbing          = "Sondage du système en cours...",
        AboutCapsCpu              = "Processeur",
        AboutCapsMemory           = "Mémoire",
        AboutCapsGpu              = "Adaptateur graphique",
        AboutCapsNpu              = "Accélérateur IA (NPU)",
        AboutCapsAiTier           = "Niveau de masquage IA v2.0",
        AboutCapsNone             = "Aucun détecté",
        AboutCapsIntegrated       = "intégré",
    };

    public static readonly StringsTable Es = new()
    {
        PrivacyActive             = "Privacidad activa",
        AutoOff                   = "auto-desact.",
        PrivacyBadge              = "• PRIVACIDAD",
        EmptyTile                 = "vacío",

        StatusConnecting          = "Conectando...",
        StatusDisconnected        = "Conexión perdida",
        StatusVideoLoss           = "Sin señal de vídeo",
        StatusError               = "Error",
        ErrorPrefix               = "Error: ",

        PinSetTitle               = "Establecer PIN de sesión",
        PinSetSubtitle            = "Este PIN es necesario para desactivar la privacidad.",
        PinVerifyTitle            = "Introducir PIN de sesión",
        PinVerifySubtitle         = "Desactivar privacidad",
        PinWrong                  = "PIN incorrecto. Inténtelo de nuevo.",
        PinLockedOutFormat        = "Demasiados intentos. Espere {0} segundos.",
        PinHint4Digits            = "4 dígitos",

        MenuSetup                 = "Configuración...",
        MenuExit                  = "Salir",
        MenuHelp                  = "Ayuda...",
        AdminPinFirstSetupTitle   = "Establecer PIN de administrador",
        AdminPinFirstSetupSub     = "Primera ejecución: elija un código de administrador de 4 dígitos para salir y configurar.",
        AdminPinExitTitle         = "Salir",
        AdminPinExitSub           = "Introduzca el PIN de administrador para salir del quiosco.",
        AdminPinSetupOpenTitle    = "Configuración",
        AdminPinSetupOpenSub      = "Introduzca el PIN de administrador para abrir la configuración.",

        SetupTitle                = "InteractiveMask — Configuración",
        SetupBrandSub             = "Configurar NVR, cámaras y privacidad.",
        SetupCancel               = "Cancelar",
        SetupApply                = "Aplicar",
        SetupSave                 = "Guardar",
        SetupConfigLoaded         = "Configuración cargada",
        SetupChangesApplied       = "Cambios aplicados.",
        SetupRestartPrompt        = "Los cambios en la conexión NVR, las asignaciones de cámaras o el tamaño de la cuadrícula requieren reiniciar Display. ¿Reiniciar ahora?",

        NavNvr                    = "NVR",
        NavCameras                = "Cámaras",
        NavGrid                   = "Cuadrícula",
        NavPrivacy                = "Privacidad",
        NavWeb                    = "Interfaz web",
        NavAudit                  = "Registro de auditoría",
        NavAdmin                  = "Administrador",

        NvrPageTitle              = "Conexiones NVR",
        NvrPageSubtitle           = "Uno o varios NVR. Las cámaras en la pestaña Cámaras pueden elegir cualquier NVR de esta lista.",
        NvrCardConnection         = "Lista de NVR",
        NvrFieldIp                = "Dirección IP",
        NvrFieldPort              = "Puerto",
        NvrFieldUser              = "Usuario",
        NvrFieldPassword          = "Contraseña",
        NvrPasswordHint           = "La contraseña se almacena cifrada mediante Windows DPAPI (ámbito de máquina).",
        NvrAddRow                 = "Añadir NVR",
        NvrDeleteRowTooltip       = "Eliminar NVR",
        NvrEditRowTooltip         = "Editar NVR",
        NvrHeaderName             = "NOMBRE",
        NvrMultiHint              = "Añada varios NVR para construir una cuadrícula con cámaras de diferentes grabadoras. Cada cámara en la pestaña Cámaras elige su NVR a través de la columna NVR.",
        NvrDialogTitle            = "Conexión NVR",
        NvrDialogAddTitle         = "Añadir NVR",
        NvrDialogEditTitle        = "Editar NVR",
        NvrDialogSubtitle         = "LAN directo, protocolo IDIS en el puerto 8016 (por defecto).",
        NvrDialogEditSubtitle     = "Actualice los datos de conexión.",
        NvrDialogErrIpEmpty       = "La dirección IP o el nombre del host es obligatorio.",

        CamerasPageTitle          = "Cámaras",
        CamerasPageSubtitle       = "Qué cámaras se muestran en qué ranura de la cuadrícula.",
        CamerasCardBindings       = "Asignaciones de ranura",
        CamerasAddRow             = "Añadir fila",
        CamerasHelpText           = "Ranura = posición en la cuadrícula (1..N). Elija en NVR de qué grabadora viene esta cámara. Cámara # = canal de cámara en ese NVR (comienza en 1). Elija Normal en Stream para visualización multicámara en directo; Alto consume más ancho de banda.",
        CamerasDeleteRowTooltip   = "Eliminar fila",
        CamerasReorderTooltip     = "Arrastre para reordenar la secuencia de mosaicos",
        CamerasSyncNames          = "Obtener nombres desde el NVR",
        CamerasSyncNamesProgress  = "Obteniendo nombres del NVR...",
        CamerasSyncNamesDoneFormat = "Sincronizado: {0} nombres actualizados desde el NVR.",
        CamerasSyncNamesNoneApplied = "Sincronización completada: todas las etiquetas son personalizadas y se mantienen sin cambios.",
        CamerasSyncNamesFailFormat = "Error de sincronización: {0}",
        CamerasSyncNamesNoCameras = "Añada primero filas de cámara.",
        StreamHigh                = "Alto",
        StreamDefault             = "Normal",
        StreamLow                 = "Bajo",
        CamerasHeaderSlot         = "RANURA",
        CamerasHeaderNvr          = "NVR",
        CamerasHeaderCameraIndex  = "CÁMARA #",
        CamerasHeaderStream       = "STREAM",
        CamerasHeaderNvrTitle     = "TÍTULO NVR",
        CamerasHeaderLabel        = "ETIQUETA PERSONALIZADA",

        GridPageTitle             = "Cuadrícula",
        GridPageSubtitle          = "Cuántos mosaicos se muestran en pantalla.",
        GridCardSize              = "Tamaño de la cuadrícula",
        GridTilesCountFormat      = "{0} mosaicos",
        GridChoice1Caption        = "1 mosaico (pantalla completa)",
        GridChoice4Caption        = "4 mosaicos",
        GridChoice9Caption        = "9 mosaicos",
        GridChoice16Caption       = "16 mosaicos",
        GridChoice25Caption       = "25 mosaicos",

        PrivacyPageTitle          = "Privacidad",
        PrivacyPageSubtitle       = "Política de PIN de sesión, temporizador de auto-desactivación y advertencia visual.",
        PrivacyCardSessionPin     = "PIN de sesión para operadores",
        PrivacySessionPinHelp     = "Cuando está habilitado, el sistema solicita un PIN de 4 dígitos para desactivar una máscara de privacidad. La primera máscara de una sesión establece el PIN; todas las desactivaciones posteriores en la misma sesión usan el mismo PIN. Cuando está deshabilitado, cualquier persona que use la pantalla puede desactivar una máscara inmediatamente.",
        PrivacyCardAutoOff        = "Temporizador de auto-desactivación",
        PrivacyAutoOffMinutesLabel = "Auto-desactivación (minutos, 0 = deshabilitado)",
        PrivacyWarningMinutesLabel = "Advertencia antes de auto-desactivación (minutos)",
        PrivacyAutoOffExample     = "Ejemplo: con auto-desactivación = 5 y advertencia = 2, un mosaico enmascarado a las 14:00 muestra un borde naranja parpadeante a partir de las 14:03; la máscara se elimina automáticamente a las 14:05.",
        PrivacyCardMassMask       = "Enmascarar / liberar todo (pulsación larga)",
        PrivacyCardMode           = "Visibilidad predeterminada",
        PrivacyModeOversightLabel = "Vista predeterminada",
        PrivacyModeOversightHelp  = "Los mosaicos comienzan visibles. El operador toca un mosaico para aplicar privacidad en el momento que sea necesario. Un temporizador de auto-desactivación se asegura de que una máscara no quede activa por accidente.",
        PrivacyModePrivacyLabel   = "Privacidad predeterminada",
        PrivacyModePrivacyHelp    = "Los mosaicos comienzan difuminados. El operador toca para revelar brevemente para verificación. Un temporizador de auto-restauración devuelve el mosaico a difuminado. Adecuado para oficinas, recepciones, hoteles, escuelas, centros de atención u otros sitios con configuraciones de privacidad más estrictas.",
        PrivacyDefaultRequireAuthOnRevealLabel = "Solicitar autenticación en cada revelación",
        PrivacyDefaultRequireAuthOnRevealHelp  = "Solo activo en modo privacidad predeterminada. Cuando está activado, cada toque pasa por la solicitud de PIN o AD antes de revelar el mosaico. Cuando está desactivado (recomendado), una revelación es libre porque la auto-restauración restablece la privacidad rápidamente.",
        PrivacyCardTileOverlay    = "Superposición de mosaico",
        PrivacyShowCameraLabel    = "Mostrar nombre personalizado",
        PrivacyShowCameraLabelHelp = "Muestra el nombre que el administrador escribió en la pestaña Cámaras, a la derecha de la barra de superposición del mosaico.",
        PrivacyShowNvrTitle       = "Mostrar título del NVR",
        PrivacyShowNvrTitleHelp   = "Muestra el título original de la cámara tal como está configurado en el NVR, a la izquierda de la barra de superposición del mosaico. Ambas opciones pueden estar activas a la vez; el título del NVR a la izquierda, la etiqueta personalizada a la derecha.",

        WebPageTitle              = "Interfaz web",
        WebPageSubtitle           = "Puertos, certificado y accesibilidad de red para el control por navegador.",
        WebCardPorts              = "Puertos",
        WebHttpPortLabel          = "Puerto HTTP",
        WebHttpsPortLabel         = "Puerto HTTPS (vacío = desactivado)",
        WebCardLan                = "Accesibilidad en la LAN",
        WebLanHelp                = "Cuando está deshabilitado, el WebHost solo escucha en localhost (solo esta máquina). Cuando está habilitado, también escucha en la dirección IP de la LAN para que las estaciones de trabajo de los compañeros o una tableta puedan controlar de forma remota.",
        WebCardCert               = "Certificado HTTPS",
        WebCertIntro              = "Ruta a un archivo PFX. La contraseña se almacena cifrada (DPAPI). O haga clic en 'Generar autofirmado' para crear un nuevo certificado en ProgramData.",
        WebCertPathLabel          = "Archivo PFX",
        WebCertBrowse             = "Examinar...",
        WebCertPasswordLabel      = "Contraseña",
        WebCertGenerate           = "Generar certificado autofirmado",
        WebCertRestartHint        = "Los cambios en los puertos o el certificado solo tienen efecto después de reiniciar el servicio WebHost.",

        WebAccessCardTitle        = "Acceso a la interfaz web",
        WebAccessCardDescription  = "Elija si la interfaz web es de acceso libre o si se requiere inicio de sesión. Esto es independiente de la política de PIN/AD por mosaico utilizada para desactivar una máscara de privacidad.",
        WebAccessMode             = "Modo de acceso",
        WebAccessModeOff          = "Abierto (sin inicio de sesión)",
        WebAccessModePin          = "PIN de acceso compartido",
        WebAccessModeAd           = "Inicio de sesión de Windows",
        WebAccessPin              = "PIN de acceso",
        WebAccessDomain           = "Dominio predeterminado (opcional)",

        AuditPageTitle            = "Registro de auditoría",
        AuditPageSubtitle         = "Historial completo de acciones de máscara, eventos de PIN y conexiones NVR.",
        AuditCardRecent           = "Eventos recientes",
        AuditRefresh              = "Actualizar",
        AuditExportCsv            = "Exportar a CSV...",
        AuditHeaderTimestamp      = "MARCA DE TIEMPO",
        AuditHeaderType           = "TIPO",
        AuditHeaderSlot           = "RANURA",
        AuditHeaderLabel          = "ETIQUETA",
        AuditHeaderSource         = "ORIGEN",
        AuditHeaderDetail         = "DETALLE",

        AuditCardForwarding       = "Reenvío Syslog",
        AuditSyslogEnabled        = "Reenviar eventos de auditoría a un servidor SIEM/syslog externo",
        AuditSyslogHost           = "Host",
        AuditSyslogPort           = "Puerto",
        AuditSyslogProtocol       = "Protocolo",
        AuditSyslogAppName        = "Nombre de aplicación (RFC 5424)",
        AuditSyslogFacility       = "Facility (0-23)",
        AuditSyslogTest           = "Enviar mensaje de prueba",
        AuditSyslogTesting        = "Enviando...",
        AuditSyslogTestOk         = "Mensaje de prueba enviado.",
        AuditSyslogTestFailFormat = "Error al enviar: {0}",
        AuditSyslogTestNoHost     = "Introduzca primero un host.",

        AdminPageTitle            = "Administrador",
        AdminPageSubtitle         = "Acceso para salir y configurar, y preferencia de idioma.",
        AdminCardPin              = "PIN de administrador",
        AdminPinDescription       = "Requerido al salir del quiosco y al abrir la configuración. Cuatro dígitos.",
        AdminPinChangeButton      = "Establecer nuevo PIN de administrador...",
        AdminCardLanguage         = "Idioma de la interfaz de usuario",
        AdminLangNl               = "Holandés",
        AdminLangEn               = "Inglés",
        AdminLanguageHint         = "Los cambios se aplican inmediatamente a todos los textos visibles.",
        AdminCardIdentity         = "Identidad de operadores",
        AdminIdentityDescription  = "Cuando está habilitado, el sistema solicita credenciales de Windows (LogonUser) en cada revelación en lugar de un PIN de sesión compartido. El registro de auditoría registra el nombre de usuario de quien desactivó la máscara — útil para la rendición de cuentas y el cumplimiento.",
        AdminDomainLabel          = "Dominio predeterminado (opcional)",
        AdminDomainHint           = "Déjelo vacío para usar la máquina actual / controlador de dominio. Los operadores también pueden escribir DOMINIO\\usuario o usuario@dominio.",
        AdminCardKiosk            = "Modo quiosco",
        AdminKioskDescription     = "Cuando está habilitado, el sistema bloquea la tecla Win, Alt+Tab, Alt+F4, Alt+Esc y Ctrl+Esc, y mantiene la ventana de Display por encima de todas las demás. El administrador siempre puede salir del quiosco mediante clic derecho y PIN de administrador. Ctrl+Alt+Supr no se puede bloquear en modo de usuario; use la directiva de grupo en el host.",
        AdminCardHelp             = "Ayuda",
        AdminHelpDescription      = "Abra el manual integrado para operadores. Útil al usar el sistema por primera vez o al formar a nuevos compañeros.",
        AdminHelpOpen             = "Abrir manual",

        SetupErrPort              = "El puerto debe ser un número.",
        SetupErrAutoMinutes       = "El temporizador de auto-desactivación debe ser 0 o mayor.",
        SetupErrWarningMinutes    = "Los minutos de advertencia deben ser 0 o mayores.",
        SetupErrHttpPort          = "El puerto HTTP debe ser un número positivo.",
        SetupErrHttpsPort         = "El puerto HTTPS debe ser un número positivo (o vacío para desactivado).",
        SetupErrDuplicateSlotFormat   = "La ranura {0} se usa más de una vez. Cada posición de la cuadrícula solo puede contener una cámara.",
        SetupErrDuplicateCameraFormat = "La cámara {0} aparece más de una vez. Cada cámara solo puede colocarse en una posición.",
        SetupErrDuplicateNvrFormat    = "El ID de NVR {0} aparece más de una vez. Cada NVR debe tener un ID único.",
        SetupErrCameraOrphanFormat    = "La ranura {0} hace referencia a un NVR que ya no está en la lista. Seleccione un NVR válido.",
        SetupErrNvrInUseFormat        = "El NVR \"{0}\" está en uso por una o más cámaras. Mueva esas cámaras a otro NVR antes de eliminar este.",
        SetupErrNoNvr                 = "Añada al menos un NVR antes de guardar.",
        SetupErrSaveFailedFormat  = "Error al guardar: {0}",
        SetupCertCreatedFormat    = "Certificado autofirmado creado: {0}",
        SetupCertCreateFailedFormat = "No se pudo crear el certificado: {0}",
        SetupAdminPinUpdated      = "PIN de administrador actualizado.",
        SetupExportedFormat       = "Exportado: {0}",
        SetupExportFailedFormat   = "Error en la exportación: {0}",
        SetupBrowseCertTitle      = "Seleccionar archivo PFX",
        SetupBrowseCertFilter     = "Certificado (*.pfx;*.p12)|*.pfx;*.p12|Todos los archivos (*.*)|*.*",
        SetupAuditExportTitle     = "Exportar registro de auditoría",
        SetupAuditExportFilter    = "CSV (*.csv)|*.csv",
        SetupAdminPinNewTitle     = "Nuevo PIN de administrador",
        SetupAdminPinNewSub       = "Establezca un nuevo código de administrador de 4 dígitos.",
        SetupAdLoginSubtitle      = "Inicie sesión con su cuenta de Windows.",
        SetupAdRemoveTitle        = "Desactivar privacidad",

        MainNoConfigLine          = "Falta la configuración de NVR. Clic derecho para Configuración / Salir.",

        ConnectionLost            = "Conexión con NVR perdida",
        ConnectionLostFormat      = "{0} — reintentando en {1} s...",
        ConnectionAttempting      = "Conectando al NVR...",

        MassUnmaskConfirmTitle    = "¿Levantar la privacidad de todos los mosaicos?",
        MassUnmaskConfirmBody     = "Esta acción elimina todas las máscaras de privacidad a la vez. Pulse OK para continuar, o Cancelar para gestionar cada mosaico individualmente.",
        MassUnmaskAuthTitle       = "Liberar todo",
        PrivacyShowMassUnmaskConfirmLabel = "Mostrar confirmación antes de liberar todo",
        PrivacyShowMassUnmaskConfirmHelp  = "Muestra un diálogo antes de que una pulsación larga levante todas las máscaras de privacidad a la vez. Recomendado para instalaciones donde el modo de autenticación está desactivado.",
        PrivacyLongPressHelp      = "Mantenga pulsado el botón del ratón durante medio segundo en cualquier parte de la pantalla para enmascarar o liberar todos los mosaicos a la vez. La pulsación simple sigue funcionando individualmente.",

        FirstRunLanguageTitle     = "Elija su idioma",
        FirstRunLanguageSubtitle  = "Choose your language / Kies je taal / Wähle deine Sprache / Choisissez votre langue / Elige tu idioma",
        FirstRunLanguageContinue  = "Continuar",

        NavAbout                  = "Acerca de",
        AboutPageTitle            = "Acerca de InteractiveMask",
        AboutPageSubtitle         = "Versión, licencias y componentes de código abierto.",
        AboutCardProduct          = "Producto",
        AboutVersionLabel         = "Versión",
        AboutPublisherLabel       = "Editor",
        AboutCopyrightLabel       = "Copyright",
        AboutWebsiteLabel         = "Sitio web",
        AboutDescription          = "InteractiveMask es una aplicación de quiosco para Windows destinada a la supervisión de vídeo en directo con protección de privacidad por mosaico. Adecuada para oficinas, recepciones, hoteles, escuelas, centros de atención y otros entornos donde la supervisión continua y la privacidad deben convivir.",
        AboutCardLicensesThird    = "Componentes de código abierto",
        AboutCardLicensesIntegrations = "Integraciones",
        AboutCardBugReport        = "Reportar un error",
        AboutBugReportIntro       = "¿Encontró un problema? Envíenos un correo electrónico con una breve descripción y, si es posible, una captura de pantalla. Cada informe se toma en serio y tratamos de responder lo más rápido posible.",
        AboutBugReportButton      = "Abrir cliente de correo",
        AboutCardSystemCapabilities = "Características del sistema",
        AboutCapsRefreshButton    = "Sondear de nuevo",
        AboutCapsProbing          = "Sondeando sistema...",
        AboutCapsCpu              = "Procesador",
        AboutCapsMemory           = "Memoria",
        AboutCapsGpu              = "Adaptador gráfico",
        AboutCapsNpu              = "Acelerador de IA (NPU)",
        AboutCapsAiTier           = "Nivel de enmascaramiento IA v2.0",
        AboutCapsNone             = "Ninguno detectado",
        AboutCapsIntegrated       = "integrada",
    };
}
