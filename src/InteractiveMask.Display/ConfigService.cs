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
        if (stored.Nvr is not null)
        {
            settings.Nvr.Ip = stored.Nvr.Ip;
            settings.Nvr.Port = stored.Nvr.Port;
            settings.Nvr.User = stored.Nvr.User;
            settings.Nvr.Password = DecryptPassword(stored.Nvr.PasswordEncrypted) ?? settings.Nvr.Password;
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
        }
        if (stored.Cameras is not null)
        {
            settings.Cameras = stored.Cameras
                .Select(c => new CameraSlotSettings
                {
                    Slot = c.Slot,
                    CameraIndex = c.CameraIndex,
                    StreamId = c.StreamId,
                    Label = c.Label ?? "",
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
        }
        if (stored.Auth is not null)
        {
            settings.Auth.UseActiveDirectory = stored.Auth.UseActiveDirectory;
            settings.Auth.Domain = stored.Auth.Domain ?? "";
        }
        if (!string.IsNullOrEmpty(stored.Language))
        {
            settings.Language = stored.Language!;
        }
    }

    private static StoredConfig ToStored(AppSettings settings) => new()
    {
        Nvr = new StoredNvr
        {
            Ip = settings.Nvr.Ip,
            Port = settings.Nvr.Port,
            User = settings.Nvr.User,
            PasswordEncrypted = EncryptPassword(settings.Nvr.Password),
        },
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
        },
        Cameras = settings.Cameras
            .Select(c => new StoredCamera
            {
                Slot = c.Slot,
                CameraIndex = c.CameraIndex,
                StreamId = c.StreamId,
                Label = c.Label,
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
        },
        Auth = new StoredAuth
        {
            UseActiveDirectory = settings.Auth.UseActiveDirectory,
            Domain = settings.Auth.Domain,
        },
        Language = settings.Language,
    };

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
        public StoredNvr? Nvr { get; set; }
        public StoredGrid? Grid { get; set; }
        public StoredPrivacy? Privacy { get; set; }
        public List<StoredCamera>? Cameras { get; set; }
        public StoredKiosk? Kiosk { get; set; }
        public StoredWeb? Web { get; set; }
        public StoredAuth? Auth { get; set; }
        public string? Language { get; set; }
    }

    private sealed class StoredAuth
    {
        public bool UseActiveDirectory { get; set; }
        public string? Domain { get; set; }
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
    }

    private sealed class StoredNvr
    {
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
    }

    private sealed class StoredCamera
    {
        public int Slot { get; set; }
        public int CameraIndex { get; set; }
        public int StreamId { get; set; } = 1;
        public string Label { get; set; } = "";
    }
}
