using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>Rounded Fluent-style surface card. Children inherit the card's
/// background color; keep them inset past the corner radius.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal class CardPanel : Panel
{
    protected const float CornerRadius = 8f;

    public CardPanel()
    {
        DoubleBuffered = true;
        // Full repaint on any size change — cards resize live now, and partial
        // invalidation smears the rounded border across the exposed area.
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = ModernTheme.CardBackground;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ModernTheme.EffectiveBackColor(Parent));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
        using GraphicsPath path = ModernTheme.RoundedPath(rect, CornerRadius);
        using (var fill = new SolidBrush(ModernTheme.CardBackground))
            g.FillPath(fill, path);
        using var border = new Pen(ModernTheme.CardBorder)
        {
            Alignment = System.Drawing.Drawing2D.PenAlignment.Inset,
        };
        g.DrawPath(border, path);
    }
}
