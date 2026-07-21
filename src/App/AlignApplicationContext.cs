using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>Message-loop context for --align: exits when the session's overlays close.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class AlignApplicationContext : ApplicationContext
{
    public AlignApplicationContext(AlignSessionCoordinator coordinator)
        => coordinator.SessionClosed += ExitThread;
}
