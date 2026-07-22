using System.Runtime.Versioning;
using Seamline.Core;

namespace Seamline.App;

/// <summary>Label-above-bold-value rows describing one monitor, live-updating,
/// sized to content. Overlay-only since M8 (the WPF cards do their own layout);
/// manual DPI scaling from deviceDpi, pinned light colors like its host.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MonitorInfoRows : Panel
{
    private readonly MonitorInfo _monitor;
    private readonly Label _resolutionValue;
    private readonly Label _positionValue;
    private readonly Label _orientationValue;
    private readonly Label _primaryValue;

    public MonitorInfoRows(MonitorInfo monitor, int deviceDpi = 96)
    {
        _monitor = monitor;
        float scale = deviceDpi / 96f;
        var labelFont = ModernTheme.UiFont(8f * scale);
        var valueFont = ModernTheme.UiFont(9f * scale, FontStyle.Bold);
        int x = (int)(4 * scale);
        int y = (int)(4 * scale);

        Label AddRow(string caption, string value)
        {
            var captionLabel = new Label
            {
                Text = caption, Font = labelFont, AutoSize = true, Location = new Point(x, y),
                // Pinned secondary gray (not ModernTheme.TextSecondary — no theming here).
                ForeColor = Color.FromArgb(160, 160, 160),
            };
            Controls.Add(captionLabel);
            y += captionLabel.PreferredHeight;
            var valueLabel = new Label
            {
                Text = value, Font = valueFont, AutoSize = true, Location = new Point(x, y),
            };
            Controls.Add(valueLabel);
            y += valueLabel.PreferredHeight + (int)(6 * scale);
            return valueLabel;
        }

        AddRow("DEVICE NAME:", monitor.DeviceName);
        AddRow("MONITOR NAME:", monitor.MonitorName);
        AddRow("FRIENDLY MONITOR NAME:", monitor.FriendlyMonitorName);
        AddRow("DISPLAY ADAPTER:", monitor.DisplayAdapter);
        _resolutionValue = AddRow("SCREEN RESOLUTION:", ResolutionText());
        _positionValue = AddRow("VIRTUAL SCREEN POSITION:", PositionText());
        _orientationValue = AddRow("ORIENTATION:", OrientationText(monitor.Orientation));
        _primaryValue = AddRow("IS PRIMARY:", monitor.IsPrimary ? "True" : "False");

        // Width from the widest row so long adapter/monitor names never clip.
        int contentWidth = Controls.Cast<Control>().Max(c => c.Right);
        Size = new Size(
            Math.Max(contentWidth, (int)(200 * scale)) + (int)(8 * scale),
            y + (int)(4 * scale));

        monitor.PropertyChanged += OnMonitorChanged;
        Disposed += (_, _) =>
        {
            monitor.PropertyChanged -= OnMonitorChanged;
            labelFont.Dispose();
            valueFont.Dispose();
        };
    }

    private void OnMonitorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _resolutionValue.Text = ResolutionText();
        _positionValue.Text = PositionText();
        _orientationValue.Text = OrientationText(_monitor.Orientation);
        _primaryValue.Text = _monitor.IsPrimary ? "True" : "False";
    }

    private string ResolutionText() => $"{_monitor.ScreenWidth}x{_monitor.ScreenHeight}";

    private string PositionText() => $"({_monitor.PositionX}, {_monitor.PositionY})";

    internal static string OrientationText(MonitorOrientation orientation) => orientation switch
    {
        MonitorOrientation.Landscape => "Landscape",
        MonitorOrientation.Portrait => "Portrait",
        MonitorOrientation.FlippedLandscape => "Landscape (flipped)",
        MonitorOrientation.FlippedPortrait => "Portrait (flipped)",
        _ => orientation.ToString(),
    };
}
