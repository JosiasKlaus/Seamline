using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Data;
using Seamline.Core;

namespace Seamline.App;

/// <summary>Device name → display index for the chip/tile ("\\.\DISPLAY2" → "2",
/// otherwise "?"), matching the overview tiles.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class DisplayIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        const string prefix = @"\\.\DISPLAY";
        return value is string name && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..]
            : "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Card title: the friendly monitor name, falling back to the raw
/// monitor name. Names are immutable per MonitorInfo instance, so binding the
/// whole object (no change notification) is safe — cards rebuild on membership
/// changes.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MonitorTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MonitorInfo monitor
            ? monitor.FriendlyMonitorName.Length > 0 ? monitor.FriendlyMonitorName : monitor.MonitorName
            : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Collapses an element bound to an empty string (the secondary
/// monitor-name line, shown only when a friendly name took the title).</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>(Orientation, IsPrimary) → "Landscape  ·  Primary monitor" line,
/// reusing the shared orientation wording.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class OrientationLineConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [MonitorOrientation orientation, bool isPrimary])
            return "";
        return MonitorInfoRows.OrientationText(orientation)
            + (isPrimary ? "  ·  Primary monitor" : "");
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
