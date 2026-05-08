using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace InteractiveMask.Display;

/// <summary>
/// First-run language picker. Shown when no <c>config.json</c> exists yet
/// (so we have nothing persisted) and the host's culture is not one we
/// can already pick automatically. Persists nothing itself: the caller
/// reads <see cref="SelectedCode"/> and either applies the language for
/// the running session or feeds it into the first <c>SaveConfig</c>.
/// </summary>
public partial class LanguagePickerDialog : Window
{
    /// <summary>BCP-47 / two-letter code of the chosen language ("nl", "en", ...).</summary>
    public string SelectedCode { get; private set; } = "nl";

    public LanguagePickerDialog(string suggestedCode)
    {
        InitializeComponent();

        var supported = Strings.Instance.SupportedLanguages;
        LangList.ItemsSource = supported;

        // Pre-select either the suggested code (typically the running app's
        // current culture) or fall back to "nl" so the list always boots with
        // a real selection. SelectedValue keys off the Code property thanks
        // to SelectedValuePath="Code" in XAML.
        var match = supported.FirstOrDefault(l =>
            string.Equals(l.Code, suggestedCode, StringComparison.OrdinalIgnoreCase));
        LangList.SelectedValue = match?.Code ?? "nl";

        Loaded += (_, _) => LangList.Focus();
    }

    /// <summary>
    /// Convenience: returns the closest match from <see cref="Strings.SupportedLanguages"/>
    /// for the running OS culture, or "nl" if the OS culture isn't in the list.
    /// </summary>
    public static string SuggestFromOsCulture()
    {
        var iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var supported = Strings.Instance.SupportedLanguages;
        return supported.Any(l => string.Equals(l.Code, iso, StringComparison.OrdinalIgnoreCase))
            ? iso
            : "nl";
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Commit();
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        Commit();
    }

    private void Commit()
    {
        if (LangList.SelectedValue is string code && !string.IsNullOrEmpty(code))
        {
            SelectedCode = code;
        }
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // No Escape branch on purpose: this dialog is the very first prompt
        // the user sees; without a language choice the rest of the UI is
        // unreadable, so we deliberately give them no Cancel path. They can
        // still close the window from the OS chrome (Alt+F4) but that quits
        // the whole app, which is the intended fallback.
        if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
    }
}
