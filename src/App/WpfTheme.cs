using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace Seamline.App;

/// <summary>Bridges the shared ModernTheme palette (System.Drawing based,
/// dark/light aware) into WPF brushes. Templated visuals reference the brushes
/// via DynamicResource, so re-running <see cref="ApplyTo"/> on a theme switch
/// retints everything by replacing the dictionary entries.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal static class WpfTheme
{
    public static void ApplyTo(ResourceDictionary resources)
    {
        Set(resources, "AccentBrush", ModernTheme.Accent);
        Set(resources, "AccentForegroundBrush", ModernTheme.AccentForeground);
        Set(resources, "CardBackgroundBrush", ModernTheme.CardBackground);
        Set(resources, "CardBorderBrush", ModernTheme.CardBorder);
        Set(resources, "TileFillBrush", ModernTheme.TileFill);
        Set(resources, "TextPrimaryBrush", ModernTheme.TextPrimary);
        Set(resources, "TextSecondaryBrush", ModernTheme.TextSecondary);

        // The Fluent AccentButtonStyle paints with the RAW system accent, which
        // ModernTheme deliberately gates behind a luminance band (readability —
        // see M7). Its state brushes are DynamicResource lookups, so shadowing
        // the keys here keeps the Fluent template/states but tints the button
        // with the same accent every other Seamline surface uses. Keys not set
        // (disabled states) fall through to the theme's own values.
        Set(resources, "AccentButtonBackground", ModernTheme.Accent);
        Set(resources, "AccentButtonBackgroundPointerOver", ModernTheme.AccentHover);
        Set(resources, "AccentButtonBackgroundPressed", ModernTheme.AccentPressed);
        Set(resources, "AccentButtonBorderBrush", ModernTheme.Accent);
        Set(resources, "AccentButtonBorderBrushPointerOver", ModernTheme.AccentHover);
        Set(resources, "AccentButtonBorderBrushPressed", ModernTheme.AccentPressed);
        Set(resources, "AccentButtonForeground", System.Drawing.Color.White);
        Set(resources, "AccentButtonForegroundPointerOver", System.Drawing.Color.White);
        Set(resources, "AccentButtonForegroundPressed", System.Drawing.Color.White);
    }

    public static MediaColor ToMedia(System.Drawing.Color color)
        => MediaColor.FromArgb(color.A, color.R, color.G, color.B);

    private static void Set(ResourceDictionary resources, string key, System.Drawing.Color color)
    {
        var brush = new SolidColorBrush(ToMedia(color));
        brush.Freeze();
        resources[key] = brush;
    }
}
