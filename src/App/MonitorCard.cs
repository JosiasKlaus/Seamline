using System.Runtime.Versioning;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// Fluent-style card describing one monitor in the main window: numbered chip
/// (accent for the primary) + name header, secondary detail lines, live-updating
/// values. Responsive: reflows its lines and height when the layout resizes it
/// (§8 M7.5 — replaces the original's "Monitor Information" group boxes).
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MonitorCard : CardPanel
{
    private readonly MonitorInfo _monitor;
    private readonly Label _titleLabel;
    private readonly Label _valuesLabel;
    private readonly Label _orientationLabel;
    private readonly List<(Label Label, int ExtraGapBefore)> _stack = [];
    private readonly List<Font> _fonts = [];
    private readonly float _scale;
    private readonly int _left;
    private readonly int _chipSize;
    private bool _built;
    private int _reflowedWidth = -1;

    /// <summary>Cards scale all geometry from deviceDpi themselves — the form's
    /// framework scaling never touches them (on DPI changes they are rebuilt,
    /// not rescaled).</summary>
    public MonitorCard(MonitorInfo monitor, int deviceDpi = 96)
    {
        _monitor = monitor;
        _scale = deviceDpi / 96f;
        _left = S(16);
        _chipSize = S(24);
        Width = S(260);

        Font titleFont = Track(ModernTheme.UiFont(10.5f, FontStyle.Bold));
        Font bodyFont = Track(ModernTheme.UiFont(9f));
        Font valueFont = Track(ModernTheme.UiFont(9.5f, FontStyle.Bold));
        Font smallFont = Track(ModernTheme.UiFont(8f));

        string title = monitor.FriendlyMonitorName.Length > 0
            ? monitor.FriendlyMonitorName
            : monitor.MonitorName;
        _titleLabel = new Label
        {
            Text = title, Font = titleFont, AutoSize = true,
            ForeColor = ModernTheme.TextPrimary,
        };
        Controls.Add(_titleLabel);

        Label AddLine(string text, Font font, Color color, int extraGapBefore = 0)
        {
            var label = new Label
            {
                Text = text, Font = font, AutoSize = true, ForeColor = color,
            };
            Controls.Add(label);
            _stack.Add((label, extraGapBefore));
            return label;
        }

        if (monitor.FriendlyMonitorName.Length > 0)
            AddLine(monitor.MonitorName, bodyFont, ModernTheme.TextSecondary);
        AddLine(monitor.DisplayAdapter, bodyFont, ModernTheme.TextSecondary);
        _valuesLabel = AddLine(ValuesText(), valueFont, ModernTheme.TextPrimary, S(4));
        _orientationLabel = AddLine(OrientationText(), bodyFont, ModernTheme.TextSecondary);
        AddLine(monitor.DeviceName, smallFont, ModernTheme.TextSecondary, S(2));

        _built = true;
        Reflow();

        monitor.PropertyChanged += OnMonitorChanged;
        Disposed += (_, _) =>
        {
            monitor.PropertyChanged -= OnMonitorChanged;
            foreach (Font font in _fonts)
                font.Dispose();
            _fonts.Clear();
        };
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Reflow(); // width comes from the layout's percent columns
    }

    /// <summary>Restacks the lines for the current width and derives the height.
    /// Guarded so the Height assignment cannot recurse.</summary>
    private void Reflow()
    {
        if (!_built || Width == _reflowedWidth)
            return;
        _reflowedWidth = Width;

        _titleLabel.MaximumSize = new Size(Math.Max(50, Width - _left - _chipSize - S(26)), 0);
        _titleLabel.Location = new Point(_left + _chipSize + S(10), S(14) + 1);
        int y = S(14) + Math.Max(_chipSize, _titleLabel.PreferredHeight) + S(10);
        foreach ((Label label, int extraGap) in _stack)
        {
            y += extraGap;
            label.MaximumSize = new Size(Math.Max(50, Width - _left - S(16)), 0);
            label.Location = new Point(_left, y);
            y += label.PreferredHeight + S(4);
        }
        Height = y + S(12);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Numbered chip, accent for the primary — mirrors the overview tiles.
        var chipRect = new RectangleF(16f * _scale, 13f * _scale, 24f * _scale, 24f * _scale);
        using (var path = ModernTheme.RoundedPath(chipRect, 5f * _scale))
        using (var fill = new SolidBrush(_monitor.IsPrimary ? ModernTheme.Accent : ModernTheme.TileFill))
            g.FillPath(fill, path);
        using var chipFont = ModernTheme.UiFontPx(13f * _scale, FontStyle.Bold);
        // NoPadding: TextRenderer's asymmetric default padding pushed the digit left.
        TextRenderer.DrawText(g, IndexText(), chipFont, Rectangle.Round(chipRect),
            _monitor.IsPrimary ? Color.White : ModernTheme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void OnMonitorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _valuesLabel.Text = ValuesText();
        _orientationLabel.Text = OrientationText();
        Invalidate(); // chip may change with IsPrimary
    }

    private int S(int logical) => (int)(logical * _scale);

    private string ValuesText()
        => $"{_monitor.ScreenWidth}×{_monitor.ScreenHeight}   ({_monitor.PositionX}, {_monitor.PositionY})";

    private string OrientationText()
        => MonitorInfoRows.OrientationText(_monitor.Orientation)
           + (_monitor.IsPrimary ? "  ·  Primary monitor" : "");

    private string IndexText()
    {
        const string prefix = @"\\.\DISPLAY";
        return _monitor.DeviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? _monitor.DeviceName[prefix.Length..]
            : "?";
    }

    private Font Track(Font font)
    {
        _fonts.Add(font);
        return font;
    }
}
