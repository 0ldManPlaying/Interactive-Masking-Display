using System;
using System.IO;
using System.Threading;

namespace InteractiveMask.Display;

/// <summary>
/// Diagnostic startup tracer for support cases. Writes one line per major
/// initialisation step to <c>%PROGRAMDATA%\InteractiveMask\bootstrap.log</c>
/// (writable by default users; <see cref="AppContext.BaseDirectory"/> in
/// Program Files is not). The file is truncated on each app launch so the
/// log always reflects the most recent boot - older entries would be noise
/// for the support engineer reading "what happened during the failed start".
/// <para>
/// Added in v2.0.1 to diagnose a fresh-install crash that exited silently
/// without writing to <c>crash.log</c>. Every call is wrapped in try/catch
/// so the tracer itself can never make a startup worse than it already is.
/// </para>
/// </summary>
public static class BootstrapLog
{
    private static readonly string _path = ResolvePath();
    private static int _initialised;

    private static string ResolvePath()
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "InteractiveMask");
            try { Directory.CreateDirectory(dir); } catch { /* swallow */ }
            return System.IO.Path.Combine(dir, "bootstrap.log");
        }
        catch
        {
            // Last-resort fallback to %TEMP% so the log lands somewhere even
            // if ProgramData is unwritable for this user.
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "interactivemask-bootstrap.log");
        }
    }

    /// <summary>
    /// Append a single step marker. <paramref name="step"/> is a short
    /// descriptor of which phase we're entering ("App.OnStartup.begin",
    /// "MainWindow.OnLoaded.before-pin-dialog", ...). Optional
    /// <paramref name="detail"/> carries extra context.
    /// </summary>
    public static void Step(string step, string? detail = null)
    {
        try
        {
            // Truncate on first call within a session so the file shows ONLY
            // the current boot. Multi-launch scenarios get a clean trace.
            if (Interlocked.Exchange(ref _initialised, 1) == 0)
            {
                try { File.WriteAllText(_path, $"=== InteractiveMask bootstrap log — {DateTimeOffset.Now:O} ==={Environment.NewLine}"); } catch { }
            }

            var line = detail is null
                ? $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {step}{Environment.NewLine}"
                : $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {step}  {detail}{Environment.NewLine}";
            File.AppendAllText(_path, line);
        }
        catch
        {
            // Tracer must never throw.
        }
    }

    /// <summary>Path used for the log; exposed so the crash handler can
    /// reference it in the user-facing error dialog.</summary>
    public static string Path => _path;
}
