using System.Text.Json;

namespace InteractiveMask.WebHost;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for Razor pages. Allocating a
/// fresh options instance per request triggers reflection-cache warm-up every
/// time, which is expensive at the polling rate of /api/access-mode and
/// /api/state. One static instance covers the whole WebHost.
/// </summary>
public static class WebJson
{
    public static JsonSerializerOptions CamelCase { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

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
    string IpcTimeout,
    string AdLoginTitle,
    string AdLoginSubtitle,
    string AdUsername,
    string AdPassword,
    string AdSignIn,
    string AdWrongCredentials,
    string SnapshotRefresh,
    string SnapshotMasked,
    string SnapshotNoFrame,
    string SnapshotError,
    string LoginTitle,
    string LoginSubtitle,
    string LoginSubPin,
    string LoginSubAd,
    string LoginPin,
    string LoginSubmit,
    string LoginWrong,
    string LoginError,
    string LoginLoading,
    string Logout)
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
        IpcTimeout:          "Geen antwoord van Display (time-out).",
        AdLoginTitle:        "Privacy uitzetten",
        AdLoginSubtitle:     "Meld je aan met je Windows-account.",
        AdUsername:          "Gebruikersnaam",
        AdPassword:          "Wachtwoord",
        AdSignIn:            "Aanmelden",
        AdWrongCredentials:  "Onjuiste gebruikersnaam of wachtwoord.",
        SnapshotRefresh:     "Snapshot vernieuwen",
        SnapshotMasked:      "Privacy actief",
        SnapshotNoFrame:     "Nog geen beeld",
        SnapshotError:       "Snapshot mislukt",
        LoginTitle:          "Aanmelden",
        LoginSubtitle:       "Toegang tot de web-interface",
        LoginSubPin:         "Voer de toegangs-PIN in.",
        LoginSubAd:          "Meld je aan met je Windows-account.",
        LoginPin:            "Toegangs-PIN",
        LoginSubmit:         "Aanmelden",
        LoginWrong:          "Onjuiste gegevens.",
        LoginError:          "Aanmelden mislukt. Probeer het opnieuw.",
        LoginLoading:        "Bezig met laden...",
        Logout:              "Afmelden");

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
        IpcTimeout:          "No response from Display (timeout).",
        AdLoginTitle:        "Disable privacy",
        AdLoginSubtitle:     "Sign in with your Windows account.",
        AdUsername:          "Username",
        AdPassword:          "Password",
        AdSignIn:            "Sign in",
        AdWrongCredentials:  "Incorrect username or password.",
        SnapshotRefresh:     "Refresh snapshot",
        SnapshotMasked:      "Privacy active",
        SnapshotNoFrame:     "No frame yet",
        SnapshotError:       "Snapshot failed",
        LoginTitle:          "Sign in",
        LoginSubtitle:       "Access to the web interface",
        LoginSubPin:         "Enter the access PIN.",
        LoginSubAd:          "Sign in with your Windows account.",
        LoginPin:            "Access PIN",
        LoginSubmit:         "Sign in",
        LoginWrong:          "Incorrect credentials.",
        LoginError:          "Sign-in failed. Please try again.",
        LoginLoading:        "Loading...",
        Logout:              "Sign out");

    public static Translations For(string? code) =>
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? En : Nl;
}
