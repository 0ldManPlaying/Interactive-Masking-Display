using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InteractiveMask.Display;

/// <summary>
/// In-app user guide for caregivers. Sidebar with eight chapters; content rendered
/// from <see cref="HelpStrings"/> in the language currently selected on
/// <see cref="Strings"/>. The window subscribes to language changes so a runtime
/// language switch (from Setup) is reflected live.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedHandler;
        Closed += OnClosedHandler;
        Strings.Instance.PropertyChanged += OnLanguageChanged;
    }

    private void OnLoadedHandler(object sender, RoutedEventArgs e) => ApplyTexts();

    private void OnClosedHandler(object? sender, System.EventArgs e)
    {
        Strings.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Strings.Current))
        {
            // Marshal to UI thread defensively in case Apply() is called from a
            // worker (today it isn't, but guarding here keeps this safe).
            if (Dispatcher.CheckAccess()) ApplyTexts();
            else Dispatcher.BeginInvoke((System.Action)ApplyTexts);
        }
    }

    /// <summary>
    /// Push every visible string for the current language into the named TextBlocks.
    /// All names are guarded with FindName-style null-checks so a partially-initialised
    /// window cannot throw.
    /// </summary>
    private void ApplyTexts()
    {
        var h = HelpStrings.ForCurrentLanguage();

        // Window chrome
        Title = h.WindowTitle;
        if (TitleBarText is not null) TitleBarText.Text = h.WindowTitle;
        if (SidebarTitle is not null) SidebarTitle.Text = h.PageTitle;
        if (SidebarSubtitle is not null) SidebarSubtitle.Text = h.PageSubtitle;
        if (CloseButton is not null) CloseButton.Content = h.ButtonClose;

        // Sidebar nav labels
        SetText(NavWelcomeText,    h.NavWelcome);
        SetText(NavScreenText,     h.NavScreen);
        SetText(NavPrivacyOnText,  h.NavPrivacyOn);
        SetText(NavPrivacyOffText, h.NavPrivacyOff);
        SetText(NavAutoOffText,    h.NavAutoOff);
        SetText(NavRemoteText,     h.NavRemote);
        SetText(NavAdminText,      h.NavAdmin);
        SetText(NavFaqText,        h.NavFaq);

        // 1. Welcome
        SetText(WelcomeTitleText,           h.WelcomeTitle);
        SetText(WelcomeSubtitleText,        h.PageSubtitle);
        SetText(WelcomeIntroText,           h.WelcomeIntro);
        SetText(WelcomeWhoForTitleText,     h.WelcomeWhoFor);
        SetText(WelcomeWhoForBodyText,      h.WelcomeWhoForBody);
        SetText(WelcomeWhatIsMaskTitleText, h.WelcomeWhatIsMaskTitle);
        SetText(WelcomeWhatIsMaskBodyText,  h.WelcomeWhatIsMaskBody);

        // 2. Screen
        SetText(ScreenTitleText,         h.ScreenTitle);
        SetText(ScreenSubtitleText,      h.PageSubtitle);
        SetText(ScreenIntroText,         h.ScreenIntro);
        SetText(ScreenStatusTitleText,   h.ScreenStatusTitle);
        SetText(ScreenStatusGreenText,   h.ScreenStatusGreen);
        SetText(ScreenStatusAmberText,   h.ScreenStatusAmber);
        SetText(ScreenStatusRedText,     h.ScreenStatusRed);
        SetText(ScreenEmptyTileText,     h.ScreenEmptyTile);
        SetText(ScreenLegendCaptionText, h.ScreenLegendCaption);
        SetText(LegendTileLiveText,       h.ScreenTileLive);
        SetText(LegendTileConnectingText, h.ScreenTileConnecting);
        SetText(LegendTileLostText,       h.ScreenTileLost);
        SetText(LegendTileMaskedText,     h.ScreenTileMasked);

        // 3. Privacy on
        SetText(PrivacyOnTitleText,    h.PrivacyOnTitle);
        SetText(PrivacyOnSubtitleText, h.PageSubtitle);
        SetText(PrivacyOnIntroText,    h.PrivacyOnIntro);
        SetText(PrivacyOnStepsText,    h.PrivacyOnSteps);
        SetText(PrivacyOnFirstPinText, h.PrivacyOnFirstPin);
        SetText(PrivacyOnTipText,      h.PrivacyOnTip);

        // 4. Privacy off
        SetText(PrivacyOffTitleText,        h.PrivacyOffTitle);
        SetText(PrivacyOffSubtitleText,     h.PageSubtitle);
        SetText(PrivacyOffIntroText,        h.PrivacyOffIntro);
        SetText(PrivacyOffPinFlowText,      h.PrivacyOffPinFlow);
        SetText(PrivacyOffAdFlowText,       h.PrivacyOffAdFlow);
        SetText(PrivacyOffLockoutTitleText, h.PrivacyOffLockoutTitle);
        SetText(PrivacyOffLockoutBodyText,  h.PrivacyOffLockoutBody);

        // 5. Auto-off
        SetText(AutoOffTitleText,        h.AutoOffTitle);
        SetText(AutoOffSubtitleText,     h.PageSubtitle);
        SetText(AutoOffIntroText,        h.AutoOffIntro);
        SetText(AutoOffWarningRingText,  h.AutoOffWarningRing);
        SetText(AutoOffNoteText,         h.AutoOffNote);
        SetText(AutoOffPhaseStartText,   h.AutoOffPhaseStart);
        SetText(AutoOffPhaseWarningText, h.AutoOffPhaseWarning);
        SetText(AutoOffPhaseDoneText,    h.AutoOffPhaseDone);

        // 6. Remote
        SetText(RemoteTitleText,    h.RemoteTitle);
        SetText(RemoteSubtitleText, h.PageSubtitle);
        SetText(RemoteIntroText,    h.RemoteIntro);
        SetText(RemoteUrlNoteText,  h.RemoteUrlNote);
        SetText(RemoteSamePinText,  h.RemoteSamePin);
        SetText(RemoteSharedText,   h.RemoteShared);

        // 7. Admin
        SetText(AdminTitleText,    h.AdminTitle);
        SetText(AdminSubtitleText, h.PageSubtitle);
        SetText(AdminOpeningText,  h.AdminOpening);
        SetText(AdminTabsText,     h.AdminTabs);
        SetText(AdminKioskText,    h.AdminKiosk);
        SetText(AdminAdText,       h.AdminAd);
        SetText(AdminAuditText,    h.AdminAudit);
        SetText(AdminTechNoteText, h.AdminTechNote);

        // 8. FAQ
        SetText(FaqTitleText,    h.FaqTitle);
        SetText(FaqSubtitleText, h.PageSubtitle);
        SetText(FaqQ1Text, h.FaqQ1); SetText(FaqA1Text, h.FaqA1);
        SetText(FaqQ2Text, h.FaqQ2); SetText(FaqA2Text, h.FaqA2);
        SetText(FaqQ3Text, h.FaqQ3); SetText(FaqA3Text, h.FaqA3);
        SetText(FaqQ4Text, h.FaqQ4); SetText(FaqA4Text, h.FaqA4);
        SetText(FaqQ5Text, h.FaqQ5); SetText(FaqA5Text, h.FaqA5);
        SetText(FaqQ6Text, h.FaqQ6); SetText(FaqA6Text, h.FaqA6);
    }

    private static void SetText(TextBlock? tb, string value)
    {
        if (tb is not null) tb.Text = value;
    }

    // ---- Window chrome -----------------------------------------------------

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* DragMove can throw if state changes mid-flight */ }
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // ---- Sidebar nav -> panel switching ------------------------------------

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string targetName) return;

        foreach (var name in new[]
        {
            "WelcomePanel", "ScreenPanel", "PrivacyOnPanel", "PrivacyOffPanel",
            "AutoOffPanel", "RemotePanel", "AdminPanel", "FaqPanel",
        })
        {
            if (FindName(name) is FrameworkElement fe)
            {
                fe.Visibility = name == targetName ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
