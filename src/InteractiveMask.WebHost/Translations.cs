namespace InteractiveMask.WebHost;

/// <summary>
/// Single bag of UI strings for the browser. Mirrors the Display-side
/// <c>StringsTable</c> but kept here so the WebHost has no project reference
/// to Display. Selected by the configured language code in shared config.json.
/// </summary>
public sealed record Translations(
    string ConnectedToDisplay,
    string WaitingForDisplay,
    string Connecting,
    string ConnectionLost,
    string NoVideoSignal,
    string Error,
    string PrivacyActive,
    string AutoOff,
    string PrivacyBadge,
    string EmptyTile,
    string PinSetTitle,
    string PinSetSubtitle,
    string PinVerifyTitle,
    string PinVerifySubtitle,
    string PinHint4Digits,
    string Cancel,
    string PinWrong,
    string PinLockedOutFormat,
    string IpcUnavailable,
    string IpcTimeout)
{
    public static readonly Translations Nl = new(
        ConnectedToDisplay:  "verbonden met Display",
        WaitingForDisplay:   "wachten op Display ...",
        Connecting:          "Verbinden...",
        ConnectionLost:      "Verbinding verbroken",
        NoVideoSignal:       "Geen videosignaal",
        Error:               "Fout",
        PrivacyActive:       "Privacy actief",
        AutoOff:             "auto-uit",
        PrivacyBadge:        "PRIVACY",
        EmptyTile:           "leeg",
        PinSetTitle:         "Stel sessie-PIN in",
        PinSetSubtitle:      "Deze PIN is nodig om privacy weer uit te zetten.",
        PinVerifyTitle:      "Voer sessie-PIN in",
        PinVerifySubtitle:   "Privacy uitzetten",
        PinHint4Digits:      "4 cijfers",
        Cancel:              "Annuleer",
        PinWrong:            "Onjuiste PIN. Probeer opnieuw.",
        PinLockedOutFormat:  "Te veel pogingen. Wacht {0} seconden.",
        IpcUnavailable:      "Display is niet bereikbaar.",
        IpcTimeout:          "Geen antwoord van Display (time-out).");

    public static readonly Translations En = new(
        ConnectedToDisplay:  "connected to Display",
        WaitingForDisplay:   "waiting for Display ...",
        Connecting:          "Connecting...",
        ConnectionLost:      "Connection lost",
        NoVideoSignal:       "No video signal",
        Error:               "Error",
        PrivacyActive:       "Privacy active",
        AutoOff:             "auto-off",
        PrivacyBadge:        "PRIVACY",
        EmptyTile:           "empty",
        PinSetTitle:         "Set session PIN",
        PinSetSubtitle:      "This PIN is required to disable privacy.",
        PinVerifyTitle:      "Enter session PIN",
        PinVerifySubtitle:   "Disable privacy",
        PinHint4Digits:      "4 digits",
        Cancel:              "Cancel",
        PinWrong:            "Incorrect PIN. Try again.",
        PinLockedOutFormat:  "Too many attempts. Wait {0} seconds.",
        IpcUnavailable:      "Display is unavailable.",
        IpcTimeout:          "No response from Display (timeout).");

    public static Translations For(string? code) =>
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? En : Nl;
}
