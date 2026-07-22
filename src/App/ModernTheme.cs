using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>
/// M7 visual refresh: Fluent/Windows-11-flavored styling while staying on plain
/// WinForms. The accent follows the user's Windows accent color when readable.
/// The overlay surfaces stay white in every theme — they are measurement surfaces,
/// not chrome — so overlay-hosted controls pin their colors explicitly.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal static class ModernTheme
{
    private static readonly Color FallbackAccent = Color.FromArgb(15, 125, 193);
    private static Color? _accent;
    private static string? _uiFontFamily;

    /// <summary>The Windows accent color when available and inside a usable
    /// luminance band — light enough to sit on dark surfaces, dark enough to carry
    /// white text (and survive the hover lightening); the Seamline blue otherwise.
    /// Resolved once per process: retheming requires a restart, like the palette.</summary>
    public static Color Accent
    {
        get
        {
            if (_accent is null)
            {
                _accent = Seamline.Win32.SystemAccentColor.TryGet(out Color system)
                          && Luminance(system) is >= 0.15 and <= 0.62
                    ? system
                    : FallbackAccent;
            }
            return _accent.Value;
        }
    }

    public static Color AccentHover => Blend(Accent, Color.White, 0.15f);
    public static Color AccentPressed => Blend(Accent, Color.Black, 0.18f);

    /// <summary>Accent used as FOREGROUND (links, focus borders): lightened in dark
    /// mode so a dark accent never disappears against dark surfaces.</summary>
    public static Color AccentForeground => IsDark ? Blend(Accent, Color.White, 0.35f) : Accent;

    /// <summary>True when the app runs with dark system colors.</summary>
    public static bool IsDark
    {
        get
        {
#pragma warning disable WFO5001 // experimental WinForms dark-mode API, accepted for M7
            return Application.IsDarkModeEnabled;
#pragma warning restore WFO5001
        }
    }

    public static Color WindowBackground => IsDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
    public static Color CardBackground => IsDark ? Color.FromArgb(43, 43, 43) : Color.White;
    public static Color CardBorder => IsDark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(224, 224, 224);
    public static Color SurfaceHover => IsDark ? Color.FromArgb(52, 52, 52) : Color.FromArgb(246, 246, 246);
    public static Color SurfacePressed => IsDark ? Color.FromArgb(38, 38, 38) : Color.FromArgb(235, 235, 235);
    public static Color SurfaceDisabled => IsDark ? Color.FromArgb(48, 48, 48) : Color.FromArgb(238, 238, 238);
    public static Color TextPrimary => IsDark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(27, 27, 27);
    public static Color TextSecondary => IsDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);

    /// <summary>Fill for non-primary monitor tiles in the overview (the primary
    /// tile uses the accent, like Windows 11's display settings).</summary>
    public static Color TileFill => IsDark ? Color.FromArgb(64, 64, 64) : Color.FromArgb(225, 225, 225);

    /// <summary>Segoe UI Variable when available (Windows 11), Segoe UI otherwise.</summary>
    public static Font UiFont(float size, FontStyle style = FontStyle.Regular)
        => new(ResolveFamily(), size, style);

    /// <summary>Pixel-unit variant for owner-drawn text sized from pixel geometry.</summary>
    public static Font UiFontPx(float pixelSize, FontStyle style = FontStyle.Regular)
        => new(ResolveFamily(), pixelSize, style, GraphicsUnit.Pixel);

    private static string ResolveFamily()
    {
        if (_uiFontFamily is null)
        {
            using var probe = new Font("Segoe UI Variable Text", 9f);
            _uiFontFamily = probe.Name.StartsWith("Segoe UI Variable", StringComparison.OrdinalIgnoreCase)
                ? "Segoe UI Variable Text"
                : "Segoe UI";
        }
        return _uiFontFamily;
    }

    /// <summary>The nearest opaque ancestor background — Color.Transparent parents
    /// (layout panels) would otherwise blit as opaque white/black in owner paint.</summary>
    public static Color EffectiveBackColor(Control? control)
    {
        for (Control? c = control; c is not null; c = c.Parent)
        {
            if (c.BackColor.A == 255)
                return c.BackColor;
        }
        return WindowBackground;
    }

    public static GraphicsPath RoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Main-window/dialog buttons are FluentButton instances; only the
    // overlay-hosted buttons keep a style helper because they pin light colors.

    /// <summary>Buttons hosted on the always-white overlay panels: flat, light,
    /// theme-independent (pinned colors).</summary>
    public static void StyleOverlayButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        button.BackColor = Color.White;
        button.ForeColor = Color.Black;
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(255,
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static double Luminance(Color c)
        => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
}
