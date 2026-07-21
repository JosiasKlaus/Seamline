using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace Seamline.Win32;

// Windows 10 1607 is the support floor of the DPI-awareness-context APIs and of
// Seamline's distribution target (REWRITE_PLAN.md §7).
[SupportedOSPlatform("windows10.0.14393")]
public static class DpiAwareness
{
    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 pseudo-handle from the SDK headers;
    // CsWin32 does not generate the named constant. Compare only via
    // AreDpiAwarenessContextsEqual — pseudo-handles are not value-comparable.
    private static readonly DPI_AWARENESS_CONTEXT PerMonitorAwareV2 = new(-4);

    /// <summary>
    /// True when the calling thread is Per-Monitor-V2 DPI aware, i.e. all window
    /// coordinates and drawing happen in physical device pixels.
    /// </summary>
    public static bool IsPerMonitorV2 =>
        PInvoke.AreDpiAwarenessContextsEqual(
            PInvoke.GetThreadDpiAwarenessContext(),
            PerMonitorAwareV2);
}
