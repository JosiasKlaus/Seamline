namespace Seamline.App;

/// <summary>
/// Bordered child panel with a fake title bar; dragging the title bar moves the
/// panel within its parent. Base for the overlay's info and control boxes.
/// </summary>
internal abstract class DraggablePanel : Panel
{
    private Point? _dragStart;

    protected DraggablePanel(string title, int deviceDpi = 96)
    {
        float scale = deviceDpi / 96f;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Color.FromArgb(245, 245, 245);
        AutoSize = false;

        TitleBar = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = (int)(28 * scale),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding((int)(8 * scale), 0, 0, 0),
            BackColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9f * scale, FontStyle.Bold),
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
    }

    protected Label TitleBar { get; }
}
