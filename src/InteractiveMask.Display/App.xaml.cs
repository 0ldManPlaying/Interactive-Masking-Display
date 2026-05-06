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
        try
        {
            var settings = new ConfigService().Load();
            Strings.Instance.Apply(settings.Language);
        }
        catch
        {
            Strings.Instance.Apply("nl");
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
