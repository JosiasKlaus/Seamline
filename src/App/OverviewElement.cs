using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using Seamline.Core;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaPen = System.Windows.Media.Pen;
using WpfSize = System.Windows.Size;

namespace Seamline.App;

/// <summary>
/// WPF port of the overview miniature: non-interactive virtual-screen map drawn
/// like Windows 11's display settings — flat rounded tiles on a card surface,
/// the primary in the accent color, centered index numbers. The fit transform is
/// computed from scratch on every render; redraws on LayoutChanged
/// (post-apply/membership — never pending edits) and on theme switches.
/// Colors come from ModernTheme so the WinForms and WPF surfaces stay one
/// palette.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class OverviewElement : FrameworkElement
{
    private const double FitMargin = 24;
    private const double TileRadius = 6;

    private MonitorLayout? _layout;

    /// <summary>Attaches the layout; the window detaches on close via
    /// <see cref="Unbind"/> (the element lives for the app lifetime).</summary>
    public void Bind(MonitorLayout layout)
    {
        _layout = layout;
        layout.LayoutChanged += OnLayoutChanged;
    }

    public void Unbind()
    {
        if (_layout is { } layout)
            layout.LayoutChanged -= OnLayoutChanged;
        _layout = null;
    }

    private void OnLayoutChanged() => Dispatcher.BeginInvoke(InvalidateVisual);

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 2 || height <= 2)
            return;

        var cardBrush = new SolidColorBrush(WpfTheme.ToMedia(ModernTheme.CardBackground));
        var borderPen = new MediaPen(new SolidColorBrush(WpfTheme.ToMedia(ModernTheme.CardBorder)), 1);
        dc.DrawRoundedRectangle(cardBrush, borderPen,
            new Rect(0.5, 0.5, width - 1, height - 1), 8, 8);

        if (_layout is not { Count: > 0 } layout)
            return;

        // Bounding rectangle of all monitors in virtual-screen coordinates. The
        // mapping is purely proportional, so physical-pixel positions render
        // correctly in WPF's logical units.
        int vLeft = layout.Monitors.Min(m => m.PositionX);
        int vTop = layout.Monitors.Min(m => m.PositionY);
        int vRight = layout.Monitors.Max(m => m.PositionX + m.ScreenWidth);
        int vBottom = layout.Monitors.Max(m => m.PositionY + m.ScreenHeight);
        double vWidth = vRight - vLeft;
        double vHeight = vBottom - vTop;

        double scale = Math.Min(
            (width - 2 * FitMargin) / vWidth,
            (height - 2 * FitMargin) / vHeight);
        double offsetX = (width - vWidth * scale) / 2 - vLeft * scale;
        double offsetY = (height - vHeight * scale) / 2 - vTop * scale;

        var tileBrush = new SolidColorBrush(WpfTheme.ToMedia(ModernTheme.TileFill));
        var accentBrush = new SolidColorBrush(WpfTheme.ToMedia(ModernTheme.Accent));
        var textBrush = new SolidColorBrush(WpfTheme.ToMedia(ModernTheme.TextPrimary));

        foreach (MonitorInfo monitor in layout.Monitors)
        {
            var rect = new Rect(
                monitor.PositionX * scale + offsetX + 2,
                monitor.PositionY * scale + offsetY + 2,
                Math.Max(0, monitor.ScreenWidth * scale - 4),
                Math.Max(0, monitor.ScreenHeight * scale - 4));
            if (rect.Width < 4 || rect.Height < 4)
                continue;

            dc.DrawRoundedRectangle(monitor.IsPrimary ? accentBrush : tileBrush, null,
                rect, TileRadius, TileRadius);
            DrawIndex(dc, monitor, rect,
                monitor.IsPrimary ? MediaBrushes.White : textBrush);
        }
    }

    private void DrawIndex(DrawingContext dc, MonitorInfo monitor, Rect rect, MediaBrush brush)
    {
        // Same fallback as the card chips, so tile and chip always cross-reference.
        const string prefix = @"\\.\DISPLAY";
        string index = monitor.DeviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? monitor.DeviceName[prefix.Length..]
            : "?";

        double fontSize = rect.Height * 0.42;
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText Format(double size) => new(index, CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new MediaFontFamily("Segoe UI Variable Display, Segoe UI"),
                FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size, brush, pixelsPerDip);

        FormattedText probe = Format(fontSize);
        if (probe.Width > rect.Width * 0.8)
        {
            fontSize *= rect.Width * 0.8 / probe.Width;
            probe = Format(Math.Max(6, fontSize));
        }
        dc.DrawText(probe, new System.Windows.Point(
            rect.X + (rect.Width - probe.Width) / 2,
            rect.Y + (rect.Height - probe.Height) / 2));
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize) => new(0, 0);
}
