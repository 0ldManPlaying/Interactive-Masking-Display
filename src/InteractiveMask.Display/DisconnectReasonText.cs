using GDK;

namespace InteractiveMask.Display;

/// <summary>
/// Translates raw <see cref="G2DISCONNECT_REASON.TYPE"/> values into the short
/// user-facing label that appears in the status bar / status overlay. Keeps the
/// vocabulary consistent across the kiosk and the audit log.
/// </summary>
internal static class DisconnectReasonText
{
    public static string ForUser(G2DISCONNECT_REASON.TYPE reason) => reason switch
    {
        G2DISCONNECT_REASON.TYPE.LOGIN_FAIL          => "Onjuiste gebruiker of wachtwoord",
        G2DISCONNECT_REASON.TYPE.INVALID_VERSION     => "NVR-firmware niet ondersteund",
        G2DISCONNECT_REASON.TYPE.FULL_CHANNEL        => "NVR vol — geen verbinding mogelijk",
        G2DISCONNECT_REASON.TYPE.ADMIN_CLOSE         => "Verbinding gesloten door beheerder",
        G2DISCONNECT_REASON.TYPE.ADMIN_TIMEOUT       => "Verbinding verlopen",
        G2DISCONNECT_REASON.TYPE.SYS_SHUTDOWN        => "NVR is afgesloten",
        G2DISCONNECT_REASON.TYPE.NO_SERVER           => "NVR niet bereikbaar",
        G2DISCONNECT_REASON.TYPE.NET_DOWN            => "Netwerk niet beschikbaar",
        G2DISCONNECT_REASON.TYPE.NET_UNREACHABLE     => "Netwerk niet bereikbaar",
        G2DISCONNECT_REASON.TYPE.NET_NORESPONSE      => "Geen reactie van NVR",
        G2DISCONNECT_REASON.TYPE.NET_TIMEOUT         => "Time-out",
        G2DISCONNECT_REASON.TYPE.HOST_DOWN           => "NVR uit",
        G2DISCONNECT_REASON.TYPE.HOST_UNREACHABLE    => "NVR niet bereikbaar",
        G2DISCONNECT_REASON.TYPE.HOST_TIMEOUT        => "Time-out NVR",
        G2DISCONNECT_REASON.TYPE.CONN_TIMEOUT        => "Time-out bij verbinden",
        G2DISCONNECT_REASON.TYPE.CONN_RESET          => "Verbinding gereset door NVR",
        G2DISCONNECT_REASON.TYPE.CONN_ABORTED        => "Verbinding afgebroken",
        G2DISCONNECT_REASON.TYPE.CONN_CANCEL         => "Verbinding geannuleerd",
        G2DISCONNECT_REASON.TYPE.SSL_CONNECTION_FAILED => "SSL-fout",
        G2DISCONNECT_REASON.TYPE.LOGOUT              => "Verbinding gesloten",
        _                                            => $"Verbinding verbroken ({reason})",
    };
}
