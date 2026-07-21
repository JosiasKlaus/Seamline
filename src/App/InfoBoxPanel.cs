using Seamline.Core;

namespace Seamline.App;

/// <summary>"Display Information" box shown on every overlay: the shared monitor
/// rows wrapped in a draggable panel.</summary>
internal sealed class InfoBoxPanel : DraggablePanel
{
    public InfoBoxPanel(MonitorInfo monitor, int deviceDpi = 96)
        : base("Display Information", deviceDpi)
    {
        float scale = deviceDpi / 96f;
        var rows = new MonitorInfoRows(monitor, deviceDpi)
        {
            Location = new Point((int)(6 * scale), TitleBar.Height + (int)(4 * scale)),
            BackColor = Color.Transparent,
        };
        Controls.Add(rows);
        Size = new Size(
            rows.Width + (int)(12 * scale),
            TitleBar.Height + rows.Height + (int)(8 * scale));
    }
}
