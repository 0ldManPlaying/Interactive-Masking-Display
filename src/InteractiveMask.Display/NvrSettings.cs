namespace InteractiveMask.Display;

public sealed class AppSettings
{
    /// <summary>List of all NVRs the kiosk talks to. Each entry has a stable Id
    /// referenced from <see cref="CameraSlotSettings.NvrId"/>. The legacy single
    /// <see cref="Nvr"/> field is migrated into <c>Nvrs[0]</c> by ConfigService
    /// the first time an old config is loaded.</summary>
    public List<NvrSettings> Nvrs { get; set; } = new();

    /// <summary>Legacy single-NVR field. Kept on the type so existing code paths
    /// that only consult one NVR still compile during the multi-NVR migration;
    /// new code should iterate <see cref="Nvrs"/>.</summary>
    public NvrSettings Nvr { get; set; } = new();

    public GridSettings Grid { get; set; } = new();
    public List<CameraSlotSettings> Cameras { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public KioskSettings Kiosk { get; set; } = new();
    public WebSettings Web { get; set; } = new();
    public AuthSettings Auth { get; set; } = new();
    public AuditForwardSettings AuditForward { get; set; } = new();
    /// <summary>Two-letter UI language code: "nl" (default) or "en".</summary>
    public string Language { get; set; } = "nl";
}

/// <summary>
/// Real-time audit forwarding to an external SIEM via syslog (RFC 5424).
/// Adding more sinks (e.g. webhook for an EPD) is intentionally additive — keep
/// each sink behind its own enabled flag.
/// </summary>
public sealed class AuditForwardSettings
{
    public bool SyslogEnabled { get; set; }
    public string SyslogHost { get; set; } = "";
    public int SyslogPort { get; set; } = 514;
    /// <summary>"udp" (default, fire-and-forget) or "tcp" (persistent, reconnects).</summary>
    public string SyslogProtocol { get; set; } = "udp";
    /// <summary>RFC 5424 facility code; 16 = local0, the conventional choice for app audit.</summary>
    public int SyslogFacility { get; set; } = 16;
    /// <summary>RFC 5424 APP-NAME field (max 48 chars). Visible to the SIEM as the source app.</summary>
    public string SyslogAppName { get; set; } = "InteractiveMask";
}

public sealed class AuthSettings
{
    /// <summary>
    /// When true, the desktop replaces the shared session-PIN dialog with a
    /// Windows credential prompt and validates via <c>LogonUser</c>. Each unmask
    /// is authenticated individually, with the audit log recording the actual
    /// username instead of the generic "local" source.
    /// </summary>
    public bool UseActiveDirectory { get; set; }

    /// <summary>
    /// Optional default domain for the credential prompt. Empty = use the local
    /// machine / current logon domain. Users can still override via
    /// <c>DOMAIN\user</c> or <c>user@upn</c> in the username field.
    /// </summary>
    public string Domain { get; set; } = "";
}

public sealed class WebSettings
{
    public int HttpPort { get; set; } = 8080;
    public int? HttpsPort { get; set; }
    public string? CertPath { get; set; }
    /// <summary>Plain-text PFX password; ConfigService DPAPI-encrypts it on save.</summary>
    public string? CertPassword { get; set; }
    /// <summary>When false (default) Kestrel only listens on localhost; when true on 0.0.0.0.</summary>
    public bool BindAllInterfaces { get; set; }
    /// <summary>Authentication required to access the web UI itself. Decoupled from
    /// the per-tile PIN/AD policy — that one is for unmask actions; this one gates
    /// the page itself. Modes: "off" (default, open), "pin" (shared web PIN),
    /// "ad" (Windows credentials).</summary>
    public WebAccessSettings Access { get; set; } = new();
}

public sealed class WebAccessSettings
{
    /// <summary>"off" (default), "pin", or "ad".</summary>
    public string Mode { get; set; } = "off";
    /// <summary>Plain-text web-access PIN; ConfigService DPAPI-encrypts on save.
    /// Only meaningful when Mode = "pin".</summary>
    public string? Pin { get; set; }
    /// <summary>Default AD domain pre-filled in the login form. Empty = no default.</summary>
    public string Domain { get; set; } = "";
}

public sealed class KioskSettings
{
    /// <summary>
    /// When true, the application installs a low-level keyboard hook that swallows
    /// Win-key, Alt+Tab, Alt+F4, Alt+Esc and Ctrl+Esc, and stays topmost over the
    /// taskbar. Disabled by default to avoid surprising developers running the app
    /// from an IDE.
    /// </summary>
    public bool Enabled { get; set; }
}

public sealed class PrivacySettings
{
    /// <summary>Minutes after which an active mask is automatically removed. 0 = disabled.</summary>
    public int AutoUnmaskMinutes { get; set; }

    /// <summary>Minutes before auto-unmask at which the warning visual kicks in.</summary>
    public int WarningMinutes { get; set; } = 2;

    /// <summary>
    /// Whether unmasking requires a session PIN. When false, a click on a masked
    /// tile removes the mask immediately (no PIN dialog). The setup-window
    /// admin-PIN remains independent of this setting.
    /// </summary>
    public bool RequireSessionPin { get; set; } = true;
}

public sealed class NvrSettings
{
    /// <summary>Stable identifier referenced from <see cref="CameraSlotSettings.NvrId"/>.
    /// Assigned by Setup when a new NVR row is added; never reused after delete.</summary>
    public int Id { get; set; }

    /// <summary>User-facing label, shown in the NVR dropdown of the Cameras tab.</summary>
    public string Name { get; set; } = "";

    public string Ip { get; set; } = "";
    public int Port { get; set; } = 8016;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class GridSettings
{
    public int Rows { get; set; } = 4;
    public int Columns { get; set; } = 4;
}

public sealed class CameraSlotSettings
{
    public int Slot { get; set; }
    /// <summary>Identifies which NVR (by <see cref="NvrSettings.Id"/>) this binding
    /// belongs to. Defaults to 0 so legacy single-NVR configs migrate cleanly:
    /// the migrated single NVR ends up as Id=0 and existing camera entries keep
    /// pointing at it without any explicit field set.</summary>
    public int NvrId { get; set; }
    public int CameraIndex { get; set; }
    public int StreamId { get; set; } = 1;
    public string Label { get; set; } = "";
}
