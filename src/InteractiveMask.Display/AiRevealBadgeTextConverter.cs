using System;
using System.Globalization;
using System.Windows.Data;

namespace InteractiveMask.Display;

/// <summary>
/// Renders the on-tile AI-reveal badge text from <see cref="TileViewModel.AiRevealRemaining"/>:
/// <list type="bullet">
///   <item><see cref="TimeSpan.MaxValue"/> → the localised "AI off" text (indefinite reveal).</item>
///   <item>Otherwise → the localised "AI off · {0}" format with the remaining time as MM:SS
///   (or M:SS once the countdown drops below 10 minutes, which is the realistic ceiling
///   given the four duration choices).</item>
///   <item><see cref="TimeSpan.Zero"/> or negative → empty string (the badge container is
///   collapsed via its own DataTrigger; this is just defensive).</item>
/// </list>
/// Re-reads <see cref="Strings.Instance"/> on every invocation so a live language switch
/// (no kiosk restart) updates the badge on the next 1 Hz tick from
/// <see cref="MaskController"/>'s reveal-ticker.
/// </summary>
public sealed class AiRevealBadgeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan remaining || remaining <= TimeSpan.Zero)
            return string.Empty;

        var t = Strings.Instance.Current;

        if (remaining == TimeSpan.MaxValue)
            return t.AiRevealBadgeIndefinite;

        // m:ss is enough — the longest preset choice is 5 minutes. If a future
        // build extends the duration list past 9 m 59 s, switch to mm:ss.
        string formatted = remaining.TotalMinutes >= 1
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}",
                (int)remaining.TotalMinutes, remaining.Seconds)
            : string.Format(CultureInfo.InvariantCulture, "0:{0:00}", remaining.Seconds);

        return string.Format(culture, t.AiRevealBadgeCountdownFormat, formatted);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
