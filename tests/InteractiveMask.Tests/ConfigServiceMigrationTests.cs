using InteractiveMask.Display;
using System.Text.Json;
using Xunit;

namespace InteractiveMask.Tests;

public sealed class ConfigServiceMigrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "InteractiveMask.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _configPath;

    public ConfigServiceMigrationTests()
    {
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    [Fact]
    public void Load_migrates_legacy_single_nvr_to_nvrs_array()
    {
        const string legacyJson = """
            {
              "Nvr": {
                "Ip": "192.168.1.50",
                "Port": 8016,
                "User": "admin",
                "PasswordEncrypted": null
              },
              "Grid": { "Rows": 2, "Columns": 2 }
            }
            """;
        File.WriteAllText(_configPath, legacyJson);

        var service = new ConfigService(_configPath);
        var settings = service.Load();

        Assert.Single(settings.Nvrs);
        Assert.Equal(0, settings.Nvrs[0].Id);
        Assert.Equal("192.168.1.50", settings.Nvrs[0].Ip);
        Assert.Equal(8016, settings.Nvrs[0].Port);
        Assert.Equal("admin", settings.Nvrs[0].User);
        Assert.Equal("192.168.1.50", settings.Nvr.Ip);
    }

    [Fact]
    public void Save_round_trips_migrated_nvr_as_nvrs_array()
    {
        const string legacyJson = """
            {
              "Nvr": {
                "Ip": "10.0.0.9",
                "Port": 9000,
                "User": "operator",
                "PasswordEncrypted": null
              }
            }
            """;
        File.WriteAllText(_configPath, legacyJson);

        var service = new ConfigService(_configPath);
        var settings = service.Load();
        service.Save(settings);

        using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("Nvrs", out var nvrs));
        Assert.Equal(JsonValueKind.Array, nvrs.ValueKind);
        Assert.Equal("10.0.0.9", nvrs[0].GetProperty("Ip").GetString());

        var reloaded = service.Load();
        Assert.Equal(settings.Nvrs[0].Ip, reloaded.Nvrs[0].Ip);
        Assert.Equal(settings.Nvrs[0].Port, reloaded.Nvrs[0].Port);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }
}
