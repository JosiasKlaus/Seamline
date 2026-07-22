using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>
/// Rounded card panel with a fake title bar; dragging the title bar moves the
/// panel within its parent. Base for the overlay's info and control boxes.
/// Pinned DARK colors throughout: these panels live on the always-white overlay
/// surface — the dark card carries the contrast — and must not follow the
/// system theme. The selection cue is a small accent pill next to the title.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal abstract class DraggablePanel : Panel
{
    // Lightened pinned accent: the raw Seamline blue sinks into the dark card.
    private static readonly Color AccentIndicator = Color.FromArgb(99, 170, 214);
    private static readonly Color CardFill = Color.FromArgb(43, 43, 43);
    private static readonly Color CardBorder = Color.FromArgb(58, 58, 58);
    private static readonly Color HeaderDivider = Color.FromArgb(58, 58, 58);
    private static readonly Color IdleIndicator = Color.FromArgb(110, 110, 110);

    private readonly float _scale;
    private Point? _dragStart;
    private bool _accented;

    protected DraggablePanel(string title, int deviceDpi = 96)
    {
        _scale = deviceDpi / 96f;
        DoubleBuffered = true;
        BackColor = CardFill;
        ForeColor = Color.FromArgb(235, 235, 235);
        AutoSize = false;

        TitleBar = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = S(32),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(22), 0, 0, 0),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(235, 235, 235),
            Font = ModernTheme.UiFont(9.5f * _scale, FontStyle.Bold),
            Cursor = Cursors.SizeAll,
        };
        Controls.Add(TitleBar);

        TitleBar.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _dragStart = e.Location;
        };
        TitleBar.MouseMove += (_, e) =>
        {
            if (_dragStart is { } start && Parent is { } parent)
            {
                // Clamp so the title bar always stays reachable inside the overlay.
                int x = Math.Clamp(Left + e.X - start.X, -Width + 60, parent.ClientSize.Width - 60);
                int y = Math.Clamp(Top + e.Y - start.Y, 0, parent.ClientSize.Height - TitleBar.Height);
                Location = new Point(x, y);
            }
        };
        TitleBar.MouseUp += (_, _) => _dragStart = null;
        Disposed += (_, _) => TitleBar.Font.Dispose();
    }

    protected Label TitleBar { get; }

    /// <summary>Accent the title pill (used to mark the selected overlay's boxes).</summary>
    public void SetTitleAccent(bool active)
    {
        if (_accented == active)
            return;
        _accented = active;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        // The overlay surface is always white, so the rounded corners blend in.
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
        using (GraphicsPath card = ModernTheme.RoundedPath(rect, S(8)))
        {
            using (var fill = new SolidBrush(CardFill))
                g.FillPath(fill, card);
            using var border = new Pen(CardBorder) { Alignment = PenAlignment.Inset };
            g.DrawPath(border, card);
        }

        using (var divider = new Pen(HeaderDivider))
            g.DrawLine(divider, S(1), TitleBar.Height, Width - S(1) - 1, TitleBar.Height);

        // Selection pill, vertically centered in the title strip.
        var pill = new RectangleF(S(10), (TitleBar.Height - S(14)) / 2f, S(4), S(14));
        using GraphicsPath pillPath = ModernTheme.RoundedPath(pill, pill.Width / 2f);
        using var pillFill = new SolidBrush(_accented ? AccentIndicator : IdleIndicator);
        g.FillPath(pillFill, pillPath);
    }

    private int S(int logical) => (int)(logical * _scale);
}
