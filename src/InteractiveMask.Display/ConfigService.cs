using Microsoft.Extensions.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveMask.Display;

/// <summary>
/// Persistent runtime configuration. Lives in <c>%PROGRAMDATA%\InteractiveMask\config.json</c>
/// and takes precedence over the bundled <c>appsettings.json</c> /
/// <c>appsettings.Local.json</c> (which now act as templates / dev shortcuts).
///
/// The NVR password is stored DPAPI-encrypted (LocalMachine scope) so the file
/// is unreadable on any other machine. The remainder of the config stays plain
/// JSON for inspectability.
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null, // keep PascalCase to match the AppSettings DTOs
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _path;

    public string Path => _path;
    public bool Exists => File.Exists(_path);

    public ConfigService()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InteractiveMask");
        try { Directory.CreateDirectory(dir); } catch { }
        _path = System.IO.Path.Combine(dir, "config.json");
    }

    /// <summary>
    /// Build the active settings. Order of precedence:
    ///   1. ProgramData\InteractiveMask\config.json (this service writes it)
    ///   2. appsettings.Local.json next to the exe (dev override)
    ///   3. appsettings.json next to the exe (template)
    /// </summary>
    public AppSettings Load()
    {
        var settings = new AppSettings();

        var baseDir = AppContext.BaseDirectory;
        var builder = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        builder.Build().Bind(settings);

        if (Exists)
        {
            try
            {
                var json = File.ReadAllText(_path);
                var stored = JsonSerializer.Deserialize<StoredConfig>(json, JsonOpts);
                if (stored is not null) ApplyStored(settings, stored);
            }
            catch
            {
                // Corrupt config falls back to the appsettings template; admin
                // can wipe config.json to recover.
            }
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        var stored = ToStored(settings);
        var json = JsonSerializer.Serialize(stored, JsonOpts);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _path, overwrite: true);
    }

    private static void ApplyStored(AppSettings settings, StoredConfig stored)
    {
        // Multi-NVR is the new shape. Legacy configs with a single Nvr field
        // get migrated into Nvrs[0] (Id=0) so existing camera entries (which
        // default NvrId to 0) keep working without any user action.
        if (stored.Nvrs is { Count: > 0 })
        {
            settings.Nvrs = stored.Nvrs
                .Select(n => new NvrSettings
                {
                    Id = n.Id,
                    Name = string.IsNullOrEmpty(n.Name) ? $"NVR {n.Id + 1}" : n.Name,
                    Ip = n.Ip ?? "",
                    Port = n.Port,
                    User = n.User ?? "",
                    Password = DecryptPassword(n.PasswordEncrypted) ?? "",
                })
                .ToList();
        }
        else if (stored.Nvr is not null && !string.IsNullOrEmpty(stored.Nvr.Ip))
        {
            settings.Nvrs = new List<NvrSettings>
            {
                new()
                {
                    Id = 0,
                    Name = "NVR 1",
                    Ip = stored.Nvr.Ip,
                    Port = stored.Nvr.Port,
                    User = stored.Nvr.User,
                    Password = DecryptPassword(stored.Nvr.PasswordEncrypted) ?? "",
                }
            };
        }
        // Mirror the first NVR into the legacy Nvr field so any code path that
        // hasn't been updated to iterate Nvrs still sees something sensible.
        if (settings.Nvrs.Count > 0)
        {
            var first = settings.Nvrs[0];
            settings.Nvr.Ip = first.Ip;
            settings.Nvr.Port = first.Port;
            settings.Nvr.User = first.User;
            settings.Nvr.Password = first.Password;
        }
        if (stored.Grid is not null)
        {
            settings.Grid.Rows = stored.Grid.Rows;
            settings.Grid.Columns = stored.Grid.Columns;
        }
        if (stored.Privacy is not null)
        {
            settings.Privacy.AutoUnmaskMinutes = stored.Privacy.AutoUnmaskMinutes;
            settings.Privacy.WarningMinutes = stored.Privacy.WarningMinutes;
            settings.Privacy.RequireSessionPin = stored.Privacy.RequireSessionPin;
            settings.Privacy.ShowMassUnmaskConfirm = stored.Privacy.ShowMassUnmaskConfirm;
            settings.Privacy.Mode = stored.Privacy.Mode == 1
                ? PrivacyMode.PrivacyDefault
                : PrivacyMode.OversightDefault;
            settings.Privacy.PrivacyDefaultRequireAuthOnReveal = stored.Privacy.PrivacyDefaultRequireAuthOnReveal;
            settings.Privacy.ShowCameraLabel = stored.Privacy.ShowCameraLabel;
            settings.Privacy.ShowNvrTitle = stored.Privacy.ShowNvrTitle;
        }
        if (stored.Cameras is not null)
        {
            // Dedupe on Slot and on (NvrId, CameraIndex). The same camera index
            // can legitimately appear twice across different NVRs; we only need
            // to prevent duplicates within one NVR. First entry wins.
            var seenSlot = new HashSet<int>();
            var seenCamera = new HashSet<(int nvrId, int cameraIndex)>();
            settings.Cameras = stored.Cameras
                .Where(c => seenSlot.Add(c.Slot) && seenCamera.Add((c.NvrId, c.CameraIndex)))
                .Select(c => new CameraSlotSettings
                {
                    Slot = c.Slot,
                    NvrId = c.NvrId,
                    CameraIndex = c.CameraIndex,
                    StreamId = c.StreamId,
                    Label = c.Label ?? "",
                    NvrTitle = c.NvrTitle ?? "",
                    AiEnabled = c.AiEnabled,
                    AiClasses = ParseAiClasses(c.AiClasses),
                    MaskPaddingPercent = Math.Clamp(c.MaskPaddingPercent, 0, 50),
                    AiRoiPolygon = ParseAiRoiPolygon(c.AiRoiPolygon),
                })
                .ToList();
        }
        if (stored.Kiosk is not null)
        {
            settings.Kiosk.Enabled = stored.Kiosk.Enabled;
        }
        if (stored.Web is not null)
        {
            settings.Web.HttpPort = stored.Web.HttpPort;
            settings.Web.HttpsPort = stored.Web.HttpsPort;
            settings.Web.CertPath = stored.Web.CertPath;
            settings.Web.CertPassword = DecryptPassword(stored.Web.CertPasswordEncrypted);
            settings.Web.BindAllInterfaces = stored.Web.BindAllInterfaces;
            if (stored.Web.Access is not null)
            {
                settings.Web.Access.Mode = string.IsNullOrEmpty(stored.Web.Access.Mode) ? "off" : stored.Web.Access.Mode;
                settings.Web.Access.Pin = DecryptPassword(stored.Web.Access.PinEncrypted);
                settings.Web.Access.Domain = stored.Web.Access.Domain ?? "";
            }
        }
        if (stored.Auth is not null)
        {
            settings.Auth.UseActiveDirectory = stored.Auth.UseActiveDirectory;
            settings.Auth.Domain = stored.Auth.Domain ?? "";
        }
        if (stored.AuditForward is not null)
        {
            settings.AuditForward.SyslogEnabled = stored.AuditForward.SyslogEnabled;
            settings.AuditForward.SyslogHost = stored.AuditForward.SyslogHost ?? "";
            settings.AuditForward.SyslogPort = stored.AuditForward.SyslogPort;
            settings.AuditForward.SyslogProtocol = string.IsNullOrEmpty(stored.AuditForward.SyslogProtocol) ? "udp" : stored.AuditForward.SyslogProtocol;
            settings.AuditForward.SyslogFacility = stored.AuditForward.SyslogFacility;
            settings.AuditForward.SyslogAppName = string.IsNullOrEmpty(stored.AuditForward.SyslogAppName) ? "InteractiveMask" : stored.AuditForward.SyslogAppName;
        }
        if (!string.IsNullOrEmpty(stored.Language))
        {
            settings.Language = stored.Language!;
        }
    }

    private static StoredConfig ToStored(AppSettings settings) => new()
    {
        Nvrs = settings.Nvrs
            .Select(n => new StoredNvr
            {
                Id = n.Id,
                Name = n.Name,
                Ip = n.Ip,
                Port = n.Port,
                User = n.User,
                PasswordEncrypted = EncryptPassword(n.Password),
            })
            .ToList(),
        // Keep legacy single-Nvr field in sync (mirror first entry) so code
        // that hasn't been updated to read Nvrs still sees something sensible
        // and an older build reading this config still works.
        Nvr = settings.Nvrs.Count > 0
            ? new StoredNvr
            {
                Ip = settings.Nvrs[0].Ip,
                Port = settings.Nvrs[0].Port,
                User = settings.Nvrs[0].User,
                PasswordEncrypted = EncryptPassword(settings.Nvrs[0].Password),
            }
            : null,
        Grid = new StoredGrid
        {
            Rows = settings.Grid.Rows,
            Columns = settings.Grid.Columns,
        },
        Privacy = new StoredPrivacy
        {
            AutoUnmaskMinutes = settings.Privacy.AutoUnmaskMinutes,
            WarningMinutes = settings.Privacy.WarningMinutes,
            RequireSessionPin = settings.Privacy.RequireSessionPin,
            ShowMassUnmaskConfirm = settings.Privacy.ShowMassUnmaskConfirm,
            Mode = (int)settings.Privacy.Mode,
            PrivacyDefaultRequireAuthOnReveal = settings.Privacy.PrivacyDefaultRequireAuthOnReveal,
            ShowCameraLabel = settings.Privacy.ShowCameraLabel,
            ShowNvrTitle = settings.Privacy.ShowNvrTitle,
        },
        Cameras = settings.Cameras
            .Select(c => new StoredCamera
            {
                Slot = c.Slot,
                NvrId = c.NvrId,
                CameraIndex = c.CameraIndex,
                StreamId = c.StreamId,
                Label = c.Label,
                NvrTitle = c.NvrTitle,
                AiEnabled = c.AiEnabled,
                AiClasses = c.AiClasses.Select(cls => cls.ToString()).ToList(),
                MaskPaddingPercent = c.MaskPaddingPercent,
                AiRoiPolygon = c.AiRoiPolygon.Select(p => new[] { p.X, p.Y }).ToList(),
            })
            .ToList(),
        Kiosk = new StoredKiosk { Enabled = settings.Kiosk.Enabled },
        Web = new StoredWeb
        {
            HttpPort = settings.Web.HttpPort,
            HttpsPort = settings.Web.HttpsPort,
            CertPath = settings.Web.CertPath,
            CertPasswordEncrypted = EncryptPassword(settings.Web.CertPassword ?? ""),
            BindAllInterfaces = settings.Web.BindAllInterfaces,
            Access = new StoredWebAccess
            {
                Mode = string.IsNullOrEmpty(settings.Web.Access.Mode) ? "off" : settings.Web.Access.Mode,
                PinEncrypted = EncryptPassword(settings.Web.Access.Pin ?? ""),
                Domain = settings.Web.Access.Domain,
            },
        },
        Auth = new StoredAuth
        {
            UseActiveDirectory = settings.Auth.UseActiveDirectory,
            Domain = settings.Auth.Domain,
        },
        AuditForward = new StoredAuditForward
        {
            SyslogEnabled = settings.AuditForward.SyslogEnabled,
            SyslogHost = settings.AuditForward.SyslogHost,
            SyslogPort = settings.AuditForward.SyslogPort,
            SyslogProtocol = settings.AuditForward.SyslogProtocol,
            SyslogFacility = settings.AuditForward.SyslogFacility,
            SyslogAppName = settings.AuditForward.SyslogAppName,
        },
        Language = settings.Language,
    };

    /// <summary>
    /// Parse the persisted string list of AI categories back into a typed set.
    /// Unknown entries (legacy values, typos) are skipped silently rather than
    /// throwing on load. A null / empty list yields the v2.0 default of all
    /// three classes so old configs (no field stored) get sensible behaviour.
    /// </summary>
    private static HashSet<InteractiveMask.Detection.ObjectClass> ParseAiClasses(List<string>? stored)
    {
        if (stored is null || stored.Count == 0)
        {
            return new HashSet<InteractiveMask.Detection.ObjectClass>
            {
                InteractiveMask.Detection.ObjectClass.Person,
                InteractiveMask.Detection.ObjectClass.TwoWheeler,
                InteractiveMask.Detection.ObjectClass.Vehicle,
            };
        }
        var result = new HashSet<InteractiveMask.Detection.ObjectClass>();
        foreach (var name in stored)
        {
            if (Enum.TryParse<InteractiveMask.Detection.ObjectClass>(name, ignoreCase: true, out var cls))
            {
                result.Add(cls);
            }
        }
        return result;
    }

    /// <summary>
    /// Parse the persisted ROI polygon (list of [x, y] arrays) back into a typed
    /// list of <see cref="InteractiveMask.Detection.PolygonPoint"/>. Defensive:
    /// rows with the wrong shape (not exactly 2 ints) or null entries are
    /// skipped silently. Empty / null input yields the empty default.
    /// </summary>
    private static List<InteractiveMask.Detection.PolygonPoint> ParseAiRoiPolygon(List<int[]>? stored)
    {
        if (stored is null || stored.Count == 0)
        {
            return new List<InteractiveMask.Detection.PolygonPoint>();
        }
        var result = new List<InteractiveMask.Detection.PolygonPoint>(stored.Count);
        foreach (var pair in stored)
        {
            if (pair is null || pair.Length < 2) continue;
            result.Add(new InteractiveMask.Detection.PolygonPoint(pair[0], pair[1]));
        }
        return result;
    }

    private static string? EncryptPassword(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return null;
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(encrypted);
    }

    private static string? DecryptPassword(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            var encrypted = Convert.FromBase64String(base64);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    private sealed class StoredConfig
    {
        /// <summary>Multi-NVR list (preferred). New configs only have this.</summary>
        public List<StoredNvr>? Nvrs { get; set; }
        /// <summary>Legacy single-NVR field (still read for migration).</summary>
        public StoredNvr? Nvr { get; set; }
        public StoredGrid? Grid { get; set; }
        public StoredPrivacy? Privacy { get; set; }
        public List<StoredCamera>? Cameras { get; set; }
        public StoredKiosk? Kiosk { get; set; }
        public StoredWeb? Web { get; set; }
        public StoredAuth? Auth { get; set; }
        public StoredAuditForward? AuditForward { get; set; }
        public string? Language { get; set; }
    }

    private sealed class StoredAuth
    {
        public bool UseActiveDirectory { get; set; }
        public string? Domain { get; set; }
    }

    private sealed class StoredAuditForward
    {
        public bool SyslogEnabled { get; set; }
        public string? SyslogHost { get; set; }
        public int SyslogPort { get; set; } = 514;
        public string? SyslogProtocol { get; set; } = "udp";
        public int SyslogFacility { get; set; } = 16;
        public string? SyslogAppName { get; set; } = "InteractiveMask";
    }

    private sealed class StoredKiosk
    {
        public bool Enabled { get; set; }
    }

    private sealed class StoredWeb
    {
        public int HttpPort { get; set; } = 8080;
        public int? HttpsPort { get; set; }
        public string? CertPath { get; set; }
        public string? CertPasswordEncrypted { get; set; }
        public bool BindAllInterfaces { get; set; }
        public StoredWebAccess? Access { get; set; }
    }

    private sealed class StoredWebAccess
    {
        public string? Mode { get; set; } = "off";
        public string? PinEncrypted { get; set; }
        public string? Domain { get; set; }
    }

    private sealed class StoredNvr
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public int Port { get; set; } = 8016;
        public string User { get; set; } = "";
        public string? PasswordEncrypted { get; set; }
    }

    private sealed class StoredGrid
    {
        public int Rows { get; set; } = 4;
        public int Columns { get; set; } = 4;
    }

    private sealed class StoredPrivacy
    {
        public int AutoUnmaskMinutes { get; set; }
        public int WarningMinutes { get; set; } = 2;
        public bool RequireSessionPin { get; set; } = true;
        // v1.3.0 additions. Stored as int for Mode so the JSON value matches the
        // PrivacyMode enum's underlying int (0 = OversightDefault, 1 = PrivacyDefault).
        public bool ShowMassUnmaskConfirm { get; set; }
        public int Mode { get; set; }
        public bool PrivacyDefaultRequireAuthOnReveal { get; set; }
        public bool ShowCameraLabel { get; set; } = true;
        public bool ShowNvrTitle { get; set; }
    }

    private sealed class StoredCamera
    {
        public int Slot { get; set; }
        public int NvrId { get; set; }
        public int CameraIndex { get; set; }
        public int StreamId { get; set; } = 1;
        public string Label { get; set; } = "";
        // v1.3.0: NVR-side camera title pulled via Setup > Cameras > Pull names.
        // Persisted so the tile bottom bar can show it from first frame after a
        // restart, no live device-status round-trip needed.
        public string NvrTitle { get; set; } = "";

        // v2.0: per-camera AI masking toggle and category filter. Existing configs
        // (no fields in JSON) deserialize to the initialiser defaults: AI on, all
        // three v2.0 classes enabled. Classes are stored as string-names rather than
        // an int bitmask for forward-compat: adding LicensePlate or Face in a later
        // release is just a new value in the list; old configs simply don't list it.
        public bool AiEnabled { get; set; } = true;
        public List<string> AiClasses { get; set; } = new() { "Person", "TwoWheeler", "Vehicle" };
        // v2.0 mask-padding percent (0..50). 10 means add 10% per side, so the
        // privacy blur extends slightly past the detection bbox. Defaults to 10
        // so existing configs get a small but useful padding without explicit
        // reconfiguration.
        public int MaskPaddingPercent { get; set; } = 10;
        // v2.0 ROI polygon serialised as a list of [x, y] int pairs for readability
        // in config.json. Empty list (default) means "no ROI configured".
        public List<int[]> AiRoiPolygon { get; set; } = new();
    }
}
