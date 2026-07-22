using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>TableLayoutPanel with double buffering — the plain control repaints
/// its background unbuffered on every live-resize step, which flickers.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel()
    {
        DoubleBuffered = true;
    }
}
