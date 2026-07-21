using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// Fullscreen borderless overlay covering exactly one monitor, in physical pixels.
/// AutoScaleMode.None + manual bounds keep WinForms' PMv2 rescaling machinery away
/// from the drawing surface; a window exactly covering one monitor never straddles
/// a DPI boundary. Line repaints follow monitor/settings changes; window GEOMETRY
/// only follows LayoutChanged (post-apply), never mere keypresses. Child panels are
/// created after the handle exists so they scale to the monitor's actual DPI.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class OverlayForm : Form
{
    private readonly AlignSessionCoordinator _coordinator;
    private readonly MonitorInfo _monitor;
    private readonly AlignSettings _settings;
    private readonly ToolTip _cursorTip = new();
    private readonly List<IDisposable> _subscriptions = [];
    private InfoBoxPanel? _infoBox;
    private ControlBoxPanel? _controlBox;

    public OverlayForm(AlignSessionCoordinator coordinator, MonitorInfo monitor, AlignSettings settings)
    {
        _coordinator = coordinator;
        _monitor = monitor;
        _settings = settings;

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Text = $"Seamline {monitor.DeviceName}";
        RefreshBoundsFromMonitor();

        _settings.PropertyChanged += OnSettingsChanged;
        _monitor.PropertyChanged += OnMonitorChanged;
        _subscriptions.Add(_settings.WhenChanged(nameof(AlignSettings.ShowInfoBox), UpdateInfoBoxVisibility));
        _subscriptions.Add(_settings.WhenChanged(nameof(AlignSettings.ShowCursorPosition), () =>
        {
            if (!_settings.ShowCursorPosition)
                _cursorTip.Hide(this);
        }));
    }

    public string DeviceName => _monitor.DeviceName;

    // WS_EX_TOOLWINDOW: keep overlays out of Alt-Tab, like the original's Qt.Tool flag.
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    /// <summary>Called by the coordinator on LayoutChanged (post-apply geometry update).</summary>
    public void RefreshBoundsFromMonitor()
        => SetBounds(_monitor.PositionX, _monitor.PositionY, _monitor.ScreenWidth, _monitor.ScreenHeight);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_infoBox is not null)
            return;
        // DeviceDpi is only correct for the target monitor once the handle exists.
        // Control box added first so it stays on top when dragged over the info box
        // (WinForms child z-order: collection index 0 is topmost).
        if (_monitor.IsPrimary)
        {
            _controlBox = new ControlBoxPanel(_coordinator, _settings, DeviceDpi);
            Controls.Add(_controlBox);
        }
        _infoBox = new InfoBoxPanel(_monitor, DeviceDpi);
        Controls.Add(_infoBox);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PositionBoxes();
        UpdateInfoBoxVisibility();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        if (_settings.Antialiasing)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Keep odd-width axis-aligned lines crisp instead of blurring across
            // an extra pixel row — this tool's whole point is judging 1 px seams.
            g.PixelOffsetMode = PixelOffsetMode.Half;
        }
        else
        {
            g.SmoothingMode = SmoothingMode.None;
        }

        if (_settings.ShowDiagonalLines)
        {
            using var pen = new Pen(Color.Black, 1f);
            foreach (LineSegment s in LineGeometry.DiagonalSegments(_monitor.ScreenWidth, _monitor.ScreenHeight))
                g.DrawLine(pen, s.X1, s.Y1, s.X2, s.Y2);
        }

        bool horizontalVisible = false;
        bool verticalVisible = false;
        if (_settings.ShowHorizontalLines)
        {
            int referenceCenterY = _coordinator.ReferenceCenterY;
            using var pen = new Pen(Color.Black, _settings.LineThickness);
            foreach (LineSegment s in LineGeometry.HorizontalSegments(
                         referenceCenterY, _settings.LineSpacing, _monitor.PositionY, _monitor.ScreenWidth))
                g.DrawLine(pen, s.X1, s.Y1, s.X2, s.Y2);
            horizontalVisible = LineGeometry.AnyHorizontalSegmentVisible(referenceCenterY,
                _settings.LineSpacing, _monitor.PositionY, _monitor.ScreenWidth, _monitor.ScreenHeight);
        }

        if (_settings.ShowVerticalLines)
        {
            int referenceCenterX = _coordinator.ReferenceCenterX;
            using var pen = new Pen(Color.Black, _settings.LineThickness);
            foreach (LineSegment s in LineGeometry.VerticalSegments(
                         referenceCenterX, _settings.LineSpacing, _monitor.PositionX, _monitor.ScreenHeight))
                g.DrawLine(pen, s.X1, s.Y1, s.X2, s.Y2);
            verticalVisible = LineGeometry.AnyVerticalSegmentVisible(referenceCenterX,
                _settings.LineSpacing, _monitor.PositionX, _monitor.ScreenHeight, _monitor.ScreenWidth);
        }

        // Never a silent white overlay: hint whenever lines are enabled but none of
        // them actually reach this panel.
        if ((_settings.ShowHorizontalLines || _settings.ShowVerticalLines)
            && !horizontalVisible && !verticalVisible)
            DrawOffscreenHint(g);

        // Selection = OS focus. A badge at top-center instead of an edge frame:
        // the frame sat exactly where line continuity is judged across the bezel.
        if (ContainsFocus)
            DrawSelectionBadge(g);
    }

    private void DrawSelectionBadge(Graphics g)
    {
        float scale = DeviceDpi / 96f;
        const string text = "Selected";
        using var font = new Font("Segoe UI", 10f * scale, FontStyle.Bold);
        SizeF textSize = g.MeasureString(text, font);
        float w = textSize.Width + 28f * scale;
        float h = textSize.Height + 10f * scale;
        var rect = new RectangleF((ClientSize.Width - w) / 2f, 10f * scale, w, h);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        float radius = h / 2f;
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 90, 180);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 180);
        path.CloseFigure();
        using var background = new SolidBrush(Color.FromArgb(15, 125, 193));
        g.FillPath(background, path);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(text, font, Brushes.White, rect, format);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _coordinator.NotifyOverlaySelected(_monitor);
        _infoBox?.SetTitleAccent(true);
        Invalidate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _infoBox?.SetTitleAccent(false);
        Invalidate();
    }

    private void DrawOffscreenHint(Graphics g)
    {
        float scale = DeviceDpi / 96f;
        using var font = new Font("Segoe UI", 11f * scale);
        using var brush = new SolidBrush(Color.FromArgb(120, 120, 120));
        using var format = new StringFormat { Alignment = StringAlignment.Center };
        string hint = "The alignment lines lie outside this monitor's visible area.\n"
            + "Lines follow the selected screen's row and column — click the screen you want to align.";
        if (!_settings.ShowVerticalLines)
            hint += "\nFor screens stacked above/below each other, enable vertical lines in the Control Box.";
        g.DrawString(hint, font, brush,
            new PointF(ClientSize.Width / 2f, ClientSize.Height * 0.68f), format);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Vertical keys are modifier-insensitive, like the original (Ctrl+Up still
        // moves). Horizontal keys (M6) use Shift for the 10 px step since
        // PageUp/PageDown are taken by the vertical axis.
        int horizontalStep = (keyData & Keys.Shift) != 0 ? 10 : 1;
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Escape:
                _coordinator.RequestClose();
                return true;
            case Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Left or Keys.Right
                when _monitor.IsPrimary:
                // Pass through on the primary, faithful to the original — this is what
                // keeps arrow keys working inside the control box's numeric fields.
                return base.ProcessCmdKey(ref msg, keyData);
            case Keys.Up:
                _coordinator.MoveMonitor(_monitor, 0, +1);
                return true;
            case Keys.Down:
                _coordinator.MoveMonitor(_monitor, 0, -1);
                return true;
            case Keys.PageUp:
                _coordinator.MoveMonitor(_monitor, 0, +10);
                return true;
            case Keys.PageDown:
                _coordinator.MoveMonitor(_monitor, 0, -10);
                return true;
            // Sign convention mirrors the vertical axis: the LINE follows the key
            // (Left = +deltaX shrinks local x = ref − posX, lines move left), while
            // the monitor itself moves the opposite way in Windows-settings terms —
            // exactly like Up (+deltaY) visually moving the lines up.
            case Keys.Left:
                _coordinator.MoveMonitor(_monitor, +horizontalStep, 0);
                return true;
            case Keys.Right:
                _coordinator.MoveMonitor(_monitor, -horizontalStep, 0);
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Swallowed, faithful to the original.
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_settings.ShowCursorPosition)
        {
            Point global = Cursor.Position; // physical virtual-screen coordinates under PMv2
            _cursorTip.Show($"{global.X}, {global.Y}", this, e.X + 16, e.Y + 16, 1500);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // A user-closed overlay (Alt+F4) must not orphan the session: route it
        // through the abort path, which then closes every overlay properly.
        if (!_coordinator.IsClosingOverlays && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _coordinator.RequestClose();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _settings.PropertyChanged -= OnSettingsChanged;
        _monitor.PropertyChanged -= OnMonitorChanged;
        foreach (IDisposable s in _subscriptions)
            s.Dispose();
        _subscriptions.Clear();
        _cursorTip.Dispose();
        _coordinator.HandleOverlayClosed(this);
        base.OnFormClosed(e);
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Invalidate();

    private void OnMonitorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Invalidate();

    private void UpdateInfoBoxVisibility()
    {
        if (_infoBox is not null)
            _infoBox.Visible = _settings.ShowInfoBox;
    }

    /// <summary>Info box centered; on the primary, info box + control box are centered
    /// as a unit with the control box to the right, vertically centered.</summary>
    private void PositionBoxes()
    {
        if (_infoBox is null)
            return;
        if (_controlBox is null)
        {
            _infoBox.Location = new Point(
                (ClientSize.Width - _infoBox.Width) / 2,
                (ClientSize.Height - _infoBox.Height) / 2);
            return;
        }

        const int gap = 24;
        int unitWidth = _infoBox.Width + gap + _controlBox.Width;
        int left = (ClientSize.Width - unitWidth) / 2;
        _infoBox.Location = new Point(left, (ClientSize.Height - _infoBox.Height) / 2);
        _controlBox.Location = new Point(
            left + _infoBox.Width + gap,
            (ClientSize.Height - _controlBox.Height) / 2);
    }
}
