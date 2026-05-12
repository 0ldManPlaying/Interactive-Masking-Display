using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace InteractiveMask.Display;

public partial class App : Application
{
    /// <summary>
    /// Crash log location. Moved from <see cref="AppContext.BaseDirectory"/>
    /// to <c>%PROGRAMDATA%\InteractiveMask\crash.log</c> in v2.0.1: the install
    /// folder under <c>C:\Program Files\</c> is not writable for a default
    /// (non-admin) user, so the crash handler was silently dropping every log
    /// line. ProgramData is writable for any local user. Falls back to
    /// <c>%TEMP%</c> if even ProgramData can't be opened.
    /// </summary>
    private static readonly string LogPath = ResolveCrashLogPath();

    private static string ResolveCrashLogPath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "InteractiveMask");
            try { Directory.CreateDirectory(dir); } catch { /* swallow */ }
            return Path.Combine(dir, "crash.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "interactivemask-crash.log");
        }
    }

    private static int _dialogOpen;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;
        TaskScheduler.UnobservedTaskException += OnTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        BootstrapLog.Step("App.OnStartup.begin", $"args=[{string.Join(", ", e.Args)}]  baseDir={AppContext.BaseDirectory}");
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
        //
        // ShutdownMode guard (v2.0.1): the picker is the *first* window in
        // the WPF window list; the default OnLastWindowClose would otherwise
        // trigger application shutdown the moment the picker closes, before
        // StartupUri has created MainWindow. Force OnExplicitShutdown for
        // the duration of the first-run path, then restore. This was a
        // candidate root cause for the v1.3 / v2.0 fresh-install silent
        // exit; even if it turns out not to be, the guard is correct.
        string lang = "nl";
        var originalShutdownMode = ShutdownMode;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "InteractiveMask",
                "config.json");
            bool firstRun = !File.Exists(configPath);
            BootstrapLog.Step("App.OnStartup.firstRun-check", $"firstRun={firstRun}  configPath={configPath}");

            if (firstRun)
            {
                var seeded = ReadInitialLanguageFromRegistry();
                if (!string.IsNullOrEmpty(seeded))
                {
                    BootstrapLog.Step("App.OnStartup.lang-from-registry", $"lang={seeded}");
                    lang = seeded;
                }
                else
                {
                    BootstrapLog.Step("App.OnStartup.picker-show", $"suggested={LanguagePickerDialog.SuggestFromOsCulture()}");
                    var picker = new LanguagePickerDialog(LanguagePickerDialog.SuggestFromOsCulture());
                    var dialogResult = picker.ShowDialog();
                    BootstrapLog.Step("App.OnStartup.picker-closed", $"result={dialogResult}  selected={picker.SelectedCode}");
                    lang = picker.SelectedCode;
                }
            }
            else
            {
                var settings = new ConfigService().Load();
                lang = string.IsNullOrEmpty(settings.Language)
                    ? LanguagePickerDialog.SuggestFromOsCulture()
                    : settings.Language;
                BootstrapLog.Step("App.OnStartup.lang-from-config", $"lang={lang}");
            }
        }
        catch (Exception ex)
        {
            // Fall through to "nl" so a config-load failure never blocks startup.
            BootstrapLog.Step("App.OnStartup.lang-resolve-FAILED", $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ShutdownMode = originalShutdownMode;
        }

        Strings.Instance.Apply(lang);
        BootstrapLog.Step("App.OnStartup.lang-applied", $"lang={lang}  shutdownMode={ShutdownMode}");
        BootstrapLog.Step("App.OnStartup.end  -- WPF will now create MainWindow via StartupUri");
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
        BootstrapLog.Step("App.DispatcherUnhandledException", $"{e.Exception.GetType().Name}: {e.Exception.Message}");
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
