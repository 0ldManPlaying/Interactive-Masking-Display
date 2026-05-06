namespace InteractiveMask.Gdk;

/// <summary>
/// Connection settings for a single NVR. Plain DTO so it can come from configuration,
/// the setup wizard, or test code.
/// </summary>
public sealed record NvrConnectionInfo(
    string Ip,
    int Port,
    string User,
    string Password);
