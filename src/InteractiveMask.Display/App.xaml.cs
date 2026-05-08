using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace InteractiveMask.Display;

public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    private static int _dialogOpen;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;
        TaskScheduler.UnobservedTaskException += OnTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply the configured UI language before any window builds its visual
        // tree, so static-resource bindings to Strings.Current pick the right
        // table on first render.
        //
        // Resolution order on first run (no config.json yet):
        //   1. HKLM\Software\InteractiveMask\InitialLanguage. Set by the MSI
        //      when the admin passes INTERACTIVEMASK_LANGUAGE on the msiexec
        //      command line (mass-deployment hook). When present, we skip
        //      the picker entirely and trust the deployment choice.
        //   2. First-run picker, pre-selected on the Windows UI culture.
        //
        // For follow-up runs we trust the persisted Language in config.json;
        // if it's empty (legacy config) we fall back to the Windows UI
        // culture rather than hardcoded Dutch.
        string lang = "nl";
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "InteractiveMask",
                "config.json");
            bool firstRun = !File.Exists(configPath);

            if (firstRun)
            {
                var seeded = ReadInitialLanguageFromRegistry();
                if (!string.IsNullOrEmpty(seeded))
                {
                    lang = seeded;
                }
                else
                {
                    var picker = new LanguagePickerDialog(LanguagePickerDialog.SuggestFromOsCulture());
                    picker.ShowDialog();
                    lang = picker.SelectedCode;
                }
            }
            else
            {
                var settings = new ConfigService().Load();
                lang = string.IsNullOrEmpty(settings.Language)
                    ? LanguagePickerDialog.SuggestFromOsCulture()
                    : settings.Language;
            }
        }
        catch
        {
            // Fall through to "nl" so a config-load failure never blocks startup.
        }
        Strings.Instance.Apply(lang);
    }

    /// <summary>
    /// Reads <c>HKLM\Software\InteractiveMask\InitialLanguage</c>, set by the
    /// MSI from the optional <c>INTERACTIVEMASK_LANGUAGE</c> property. Returns
    /// the value if it matches a supported language code; null otherwise.
    /// </summary>
    private static string? ReadInitialLanguageFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\InteractiveMask");
            if (key?.GetValue("InitialLanguage") is not string raw) return null;
            var code = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(code)) return null;
            return Strings.Instance.SupportedLanguages.Any(l =>
                string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
                ? code
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("UI thread", e.Exception);
        ShowFatalDialogOnce(e.Exception, "fout op UI-thread");
        e.Handled = true;
        Shutdown(1);
    }

    private void OnAppDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogException("AppDomain (fatal)", ex);
        if (ex is not null) ShowFatalDialogOnce(ex, "fatale fout");
    }

    private void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("Background task", e.Exception);
        e.SetObserved();
    }

    private static void ShowFatalDialogOnce(Exception ex, string title)
    {
        if (Interlocked.Exchange(ref _dialogOpen, 1) == 1) return;
        try
        {
            MessageBox.Show(
                $"{ex.GetType().Name}: {ex.Message}\n\nVolledige stack:\n{LogPath}",
                $"InteractiveMask - {title}",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch { /* never throw from a crash handler */ }
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
        catch { /* never throw from a crash handler */ }
    }
}
