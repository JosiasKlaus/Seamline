using System.Drawing.Drawing2D;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// Non-interactive miniature of the virtual screen: gradient monitor tiles with
/// white borders and big white index numbers on a dark background, like the
/// original. The fit transform is computed from scratch on every paint and the
/// panel repaints on LayoutChanged (post-apply/membership — never pending edits).
/// </summary>
internal sealed class OverviewPanel : Panel
{
    private const int Margin = 10;
    private static readonly Color GradientStart = Color.FromArgb(15, 125, 193);  // top-right
    private static readonly Color GradientEnd = Color.FromArgb(119, 196, 122);   // bottom-left

    private readonly MonitorLayout _layout;
    private readonly Action _invalidate;

    public OverviewPanel(MonitorLayout layout)
    {
        _layout = layout;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(100, 100, 100); // original's neutral mid-gray
        _invalidate = Invalidate;
        layout.LayoutChanged += _invalidate;
        Disposed += (_, _) => layout.LayoutChanged -= _invalidate;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_layout.Count == 0)
            return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Bounding rectangle of all monitors in virtual-screen coordinates.
        int vLeft = _layout.Monitors.Min(m => m.PositionX);
        int vTop = _layout.Monitors.Min(m => m.PositionY);
        int vRight = _layout.Monitors.Max(m => m.PositionX + m.ScreenWidth);
        int vBottom = _layout.Monitors.Max(m => m.PositionY + m.ScreenHeight);
        float vWidth = vRight - vLeft;
        float vHeight = vBottom - vTop;

        float scale = Math.Min(
            (ClientSize.Width - 2f * Margin) / vWidth,
            (ClientSize.Height - 2f * Margin) / vHeight);
        float offsetX = (ClientSize.Width - vWidth * scale) / 2f - vLeft * scale;
        float offsetY = (ClientSize.Height - vHeight * scale) / 2f - vTop * scale;

        foreach (MonitorInfo monitor in _layout.Monitors)
        {
            var rect = new RectangleF(
                monitor.PositionX * scale + offsetX,
                monitor.PositionY * scale + offsetY,
                monitor.ScreenWidth * scale,
                monitor.ScreenHeight * scale);

            using var gradient = new LinearGradientBrush(
                new PointF(rect.Right, rect.Top), new PointF(rect.Left, rect.Bottom),
                GradientStart, GradientEnd);
            g.FillRectangle(gradient, rect);

            float borderWidth = Math.Max(1f, 20f * scale);
            using var border = new Pen(Color.White, borderWidth);
            g.DrawRectangle(border,
                rect.X + borderWidth / 2f, rect.Y + borderWidth / 2f,
                rect.Width - borderWidth, rect.Height - borderWidth);

            DrawIndex(g, monitor, rect);
        }
    }

    private static void DrawIndex(Graphics g, MonitorInfo monitor, RectangleF rect)
    {
        const string prefix = @"\\.\DISPLAY";
        string index = monitor.DeviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? monitor.DeviceName[prefix.Length..]
            : "";
        if (index.Length == 0)
            return;

        // Scale the font so the index fills the tile, like the original.
        const float probeSize = 100f;
        using var probe = new Font("Arial", probeSize, GraphicsUnit.Pixel);
        SizeF measured = g.MeasureString(index, probe);
        float fontSize = probeSize * Math.Min(rect.Width / measured.Width, rect.Height / measured.Height);

        using var font = new Font("Arial", Math.Max(4f, fontSize), GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(index, font, Brushes.White, rect, format);
    }
}
