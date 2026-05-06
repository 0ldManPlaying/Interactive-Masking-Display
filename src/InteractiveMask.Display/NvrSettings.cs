namespace InteractiveMask.Display;

public sealed class AppSettings
{
    public NvrSettings Nvr { get; set; } = new();
    public GridSettings Grid { get; set; } = new();
    public List<CameraSlotSettings> Cameras { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public KioskSettings Kiosk { get; set; } = new();
    public WebSettings Web { get; set; } = new();
    public AuthSettings Auth { get; set; } = new();
    /// <summary>Two-letter UI language code: "nl" (default) or "en".</summary>
    public string Language { get; set; } = "nl";
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
    public int CameraIndex { get; set; }
    public int StreamId { get; set; } = 1;
    public string Label { get; set; } = "";
}
