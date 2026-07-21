using Seamline.Core;

namespace Seamline.App;

/// <summary>"Display Information" box shown on every overlay: labels above bold
/// values, live-updating (the virtual-screen position follows arrow-key edits).</summary>
internal sealed class InfoBoxPanel : DraggablePanel
{
    private readonly MonitorInfo _monitor;
    private readonly Label _resolutionValue;
    private readonly Label _positionValue;

    public InfoBoxPanel(MonitorInfo monitor, int deviceDpi = 96)
        : base("Display Information", deviceDpi)
    {
        _monitor = monitor;
        float scale = deviceDpi / 96f;
        var labelFont = new Font("Segoe UI", 8f * scale);
        var valueFont = new Font("Segoe UI", 9f * scale, FontStyle.Bold);

        int y = TitleBar.Height + (int)(8 * scale);
        int x = (int)(10 * scale);
        int width = (int)(300 * scale);

        Label AddRow(string caption, string value)
        {
            var captionLabel = new Label
            {
                Text = caption, Font = labelFont, AutoSize = true, Location = new Point(x, y),
                ForeColor = Color.FromArgb(96, 96, 96),
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

        AddRow("DEVICE NAME", monitor.DeviceName);
        AddRow("MONITOR NAME", monitor.MonitorName);
        AddRow("FRIENDLY MONITOR NAME", monitor.FriendlyMonitorName);
        AddRow("DISPLAY ADAPTER", monitor.DisplayAdapter);
        _resolutionValue = AddRow("SCREEN RESOLUTION", ResolutionText());
        _positionValue = AddRow("VIRTUAL SCREEN POSITION", PositionText());
        AddRow("ORIENTATION", OrientationText(monitor.Orientation));
        AddRow("IS PRIMARY", monitor.IsPrimary ? "Yes" : "No");

        Size = new Size(width, y + (int)(8 * scale));

        monitor.PropertyChanged += OnMonitorChanged;
        Disposed += (_, _) => monitor.PropertyChanged -= OnMonitorChanged;
    }

    private void OnMonitorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _resolutionValue.Text = ResolutionText();
        _positionValue.Text = PositionText();
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
