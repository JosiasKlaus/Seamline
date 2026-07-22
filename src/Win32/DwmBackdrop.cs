using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Seamline.Win32;

/// <summary>Windows 11 Mica backdrop. Best-effort: the attribute needs 22H2+;
/// failures (or Windows 10) are deliberately ignored.</summary>
[SupportedOSPlatform("windows10.0.14393")]
public static class DwmBackdrop
{
    public static void TryApplyMica(nint hwnd)
    {
        try
        {
            unsafe
            {
                DWM_SYSTEMBACKDROP_TYPE value = DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
                PInvoke.DwmSetWindowAttribute(new HWND(hwnd),
                    DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                    &value, sizeof(DWM_SYSTEMBACKDROP_TYPE));
            }
        }
        catch
        {
            // Cosmetic only.
        }
    }
}
