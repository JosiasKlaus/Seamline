using System.Drawing.Drawing2D;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// Non-interactive miniature of the virtual screen, drawn like Windows 11's own
/// display settings: flat rounded tiles on a card surface, the primary in the
/// accent color, centered index numbers (§8 M7.5 — replaces the original's
/// gradient/border look). The fit transform is computed from scratch on every
/// paint; repaints on LayoutChanged (post-apply/membership — never pending edits).
/// </summary>
internal sealed class OverviewPanel : Panel
{
    private const int FitMargin = 24;
    private const float TileRadius = 6f;

    private readonly MonitorLayout _layout;
    private readonly Action _invalidate;

    public OverviewPanel(MonitorLayout layout)
    {
        _layout = layout;
        DoubleBuffered = true;
        // Full repaint on any size change — partial invalidation smears the
        // rounded border across the newly exposed area.
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = ModernTheme.WindowBackground;
        _invalidate = Invalidate;
        layout.LayoutChanged += _invalidate;
        Disposed += (_, _) => layout.LayoutChanged -= _invalidate;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // The card surface.
        var cardRect = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
        using (GraphicsPath cardPath = ModernTheme.RoundedPath(cardRect, 8f))
        {
            using (var fill = new SolidBrush(ModernTheme.CardBackground))
                g.FillPath(fill, cardPath);
            using var borderPen = new Pen(ModernTheme.CardBorder)
            {
                Alignment = System.Drawing.Drawing2D.PenAlignment.Inset,
            };
            g.DrawPath(borderPen, cardPath);
        }

        if (_layout.Count == 0)
            return;

        // Bounding rectangle of all monitors in virtual-screen coordinates.
        int vLeft = _layout.Monitors.Min(m => m.PositionX);
        int vTop = _layout.Monitors.Min(m => m.PositionY);
        int vRight = _layout.Monitors.Max(m => m.PositionX + m.ScreenWidth);
        int vBottom = _layout.Monitors.Max(m => m.PositionY + m.ScreenHeight);
        float vWidth = vRight - vLeft;
        float vHeight = vBottom - vTop;

        float scale = Math.Min(
            (ClientSize.Width - 2f * FitMargin) / vWidth,
            (ClientSize.Height - 2f * FitMargin) / vHeight);
        float offsetX = (ClientSize.Width - vWidth * scale) / 2f - vLeft * scale;
        float offsetY = (ClientSize.Height - vHeight * scale) / 2f - vTop * scale;

        foreach (MonitorInfo monitor in _layout.Monitors)
        {
            var rect = new RectangleF(
                monitor.PositionX * scale + offsetX + 2f,
                monitor.PositionY * scale + offsetY + 2f,
                monitor.ScreenWidth * scale - 4f,
                monitor.ScreenHeight * scale - 4f);
            if (rect.Width < 4f || rect.Height < 4f)
                continue;

            // Borderless flat tiles, like Win11's display settings (and the card chips);
            // the surface gap between tiles keeps them distinct.
            using GraphicsPath tile = ModernTheme.RoundedPath(rect, TileRadius);
            using (var fill = new SolidBrush(monitor.IsPrimary ? ModernTheme.Accent : ModernTheme.TileFill))
                g.FillPath(fill, tile);

            DrawIndex(g, monitor, rect);
        }
    }

    private static void DrawIndex(Graphics g, MonitorInfo monitor, RectangleF rect)
    {
        // Same fallback as the card chips, so tile and chip always cross-reference.
        const string prefix = @"\\.\DISPLAY";
        string index = monitor.DeviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? monitor.DeviceName[prefix.Length..]
            : "?";

        // Prominent but not tile-filling — the Windows 11 display-settings look.
        float fontSize = rect.Height * 0.42f;
        using var probe = ModernTheme.UiFontPx(fontSize, FontStyle.Bold);
        SizeF measured = g.MeasureString(index, probe);
        if (measured.Width > rect.Width * 0.8f)
            fontSize *= rect.Width * 0.8f / measured.Width;

        using Font font = ModernTheme.UiFontPx(Math.Max(6f, fontSize), FontStyle.Bold);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        using var brush = new SolidBrush(monitor.IsPrimary ? Color.White : ModernTheme.TextPrimary);
        g.DrawString(index, font, brush, rect, format);
    }
}
