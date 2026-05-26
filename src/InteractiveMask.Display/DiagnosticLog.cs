using System.Diagnostics;

namespace InteractiveMask.Display;

/// <summary>
/// Lightweight diagnostic sink for non-fatal faults on critical paths. Mirrors
/// the swallow-and-continue style of <see cref="BootstrapLog"/> but writes to
/// <see cref="Debug"/> so support can capture output without adding dependencies.
/// </summary>
public static class DiagnosticLog
{
    /// <summary>Log a swallowed exception from a best-effort path.</summary>
    public static void SwallowedException(string context, Exception ex)
        => Debug.WriteLine($"[InteractiveMask] {context}: {ex.GetType().Name}: {ex.Message}");

    /// <summary>Log a short message when a silent catch block fires.</summary>
    public static void SwallowedFault(string context, string? detail = null)
        => Debug.WriteLine(detail is null
            ? $"[InteractiveMask] {context}"
            : $"[InteractiveMask] {context} — {detail}");
}
