using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveMask.Hardware;

/// <summary>
/// Persists the latest <see cref="HostCapabilityProfile"/> to
/// <c>%PROGRAMDATA%\InteractiveMask\capability-profile.json</c> so support engineers
/// can read the host's last-known capability snapshot without having to launch the UI.
/// </summary>
public static class CapabilityProfileStore
{
    private const string FolderName = "InteractiveMask";
    private const string FileName = "capability-profile.json";

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        FolderName,
        FileName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Saves the profile to <paramref name="path"/> or <see cref="DefaultPath"/>.
    /// Failures are swallowed: persistence is best-effort, never blocking.
    /// </summary>
    public static bool Save(HostCapabilityProfile profile, string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(target, JsonSerializer.Serialize(profile, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads the last-saved profile from <paramref name="path"/> or <see cref="DefaultPath"/>.
    /// Returns null when the file does not exist or cannot be parsed (e.g. schema mismatch
    /// after an upgrade).
    /// </summary>
    public static HostCapabilityProfile? Load(string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            if (!File.Exists(target)) return null;
            var json = File.ReadAllText(target);
            return JsonSerializer.Deserialize<HostCapabilityProfile>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
