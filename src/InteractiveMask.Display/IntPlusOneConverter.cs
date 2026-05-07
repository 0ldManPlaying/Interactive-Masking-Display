using System.Globalization;
using System.Windows.Data;

namespace InteractiveMask.Display;

/// <summary>
/// Two-way converter that displays a 0-indexed integer as 1-indexed in the UI
/// and converts user input back. Used for the slot and camera-number columns
/// in Setup so the user sees the natural "1..16" mental model while the
/// persisted value remains 0-based (matches the GDK and the IPC contract).
/// </summary>
public sealed class IntPlusOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return (i + 1).ToString(culture);
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, NumberStyles.Integer, culture, out var n))
        {
            return Math.Max(0, n - 1);
        }
        return value;
    }
}
