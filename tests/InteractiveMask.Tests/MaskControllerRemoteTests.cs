using InteractiveMask.Display;
using InteractiveMask.Ipc;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace InteractiveMask.Tests;

public sealed class MaskControllerRemoteTests
{
    [StaFact]
    public void HandleRemoteToggle_apply_mask_is_free_when_unmasked_and_pin_policy_off()
    {
        var (controller, tile, _) = CreateController(
            requirePin: false,
            useAd: false,
            privacyMode: PrivacyMode.OversightDefault,
            privacyDefaultRequireAuth: true);

        var result = controller.HandleRemoteToggle(tile, pin: null, source: "test");

        Assert.Equal(ToggleResult.Ok, result);
        Assert.True(tile.IsMasked);
    }

    [StaFact]
    public void HandleRemoteToggle_unmask_requires_pin_when_policy_enabled()
    {
        var (controller, tile, pinService) = CreateController(
            requirePin: true,
            useAd: false,
            privacyMode: PrivacyMode.OversightDefault,
            privacyDefaultRequireAuth: true);
        pinService.SetSessionPin("1234");
        tile.SetMasked(true);

        Assert.Equal(ToggleResult.PinRequired, controller.HandleRemoteToggle(tile, pin: null, source: "test"));
        Assert.Equal(ToggleResult.Ok, controller.HandleRemoteToggle(tile, pin: "1234", source: "test"));
        Assert.False(tile.IsMasked);
    }

    [StaFact]
    public void HandleRemoteToggle_unmask_free_when_pin_policy_disabled()
    {
        var (controller, tile, _) = CreateController(
            requirePin: false,
            useAd: false,
            privacyMode: PrivacyMode.PrivacyDefault,
            privacyDefaultRequireAuth: true);
        tile.SetMasked(true);

        var result = controller.HandleRemoteToggle(tile, pin: null, source: "test");

        Assert.Equal(ToggleResult.Ok, result);
        Assert.False(tile.IsMasked);
    }

    [StaFact]
    public void HandleRemoteToggle_ad_unmask_requires_preAuthenticated()
    {
        var (controller, tile, _) = CreateController(
            requirePin: false,
            useAd: true,
            privacyMode: PrivacyMode.OversightDefault,
            privacyDefaultRequireAuth: false);
        tile.SetMasked(true);

        Assert.Equal(ToggleResult.CredentialsRequired,
            controller.HandleRemoteToggle(tile, pin: null, source: "test", preAuthenticated: false));
        Assert.Equal(ToggleResult.Ok,
            controller.HandleRemoteToggle(tile, pin: null, source: "test", preAuthenticated: true));
        Assert.False(tile.IsMasked);
    }

    private static (MaskController Controller, TileViewModel Tile, BlurPinService PinService) CreateController(
        bool requirePin,
        bool useAd,
        PrivacyMode privacyMode,
        bool privacyDefaultRequireAuth)
    {
        var window = new Window();
        var pinService = new BlurPinService();
        var audit = new AuditLog();
        var privacy = new PrivacySettings
        {
            RequireSessionPin = requirePin,
            Mode = privacyMode,
            PrivacyDefaultRequireAuthOnReveal = privacyDefaultRequireAuth,
            AutoUnmaskMinutes = 5,
        };
        var auth = new AuthSettings { UseActiveDirectory = useAd, Domain = "TEST" };

        var controller = new MaskController(
            owner: window,
            pinService: pinService,
            audit: audit,
            autoUnmaskMinutesProvider: () => privacy.AutoUnmaskMinutes,
            requireSessionPinProvider: () => privacy.RequireSessionPin,
            authSettingsProvider: () => auth,
            privacyModeProvider: () => privacy.Mode,
            privacyDefaultRequireAuthProvider: () => privacy.PrivacyDefaultRequireAuthOnReveal,
            onStateChanged: () => { });

        var tile = new TileViewModel(slotIndex: 0, Dispatcher.CurrentDispatcher);
        SetHasCamera(tile, true);

        return (controller, tile, pinService);
    }

    private static void SetHasCamera(TileViewModel tile, bool value)
    {
        var prop = typeof(TileViewModel).GetProperty(nameof(TileViewModel.HasCamera), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(prop);
        prop!.SetValue(tile, value);
    }
}
