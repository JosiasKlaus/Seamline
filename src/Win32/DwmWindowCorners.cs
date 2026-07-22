using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Seamline.Win32;

/// <summary>Windows 11 corner-rounding control. Best-effort: the attribute does not
/// exist before Windows 11, and failures are deliberately ignored.</summary>
[SupportedOSPlatform("windows10.0.14393")]
public static class DwmWindowCorners
{
    /// <summary>Overlays must cover their monitor to the exact corner pixel —
    /// never let Windows 11 round them.</summary>
    public static void PreventRounding(nint hwnd)
        => Set(hwnd, DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND);

    /// <summary>Windows-11-style rounded corners for regular windows.</summary>
    public static void RequestRounding(nint hwnd)
        => Set(hwnd, DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);

    private static unsafe void Set(nint hwnd, DWM_WINDOW_CORNER_PREFERENCE preference)
    {
        DWM_WINDOW_CORNER_PREFERENCE value = preference;
        PInvoke.DwmSetWindowAttribute(new HWND(hwnd),
            DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
            &value, sizeof(DWM_WINDOW_CORNER_PREFERENCE));
    }
}
