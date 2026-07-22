using System.Drawing;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Seamline.Win32;

/// <summary>
/// The user's Windows accent color, so the app can follow the OS the way Fluent
/// apps do. Reads the Settings accent from the registry first (the value the OS
/// actually renders), with DWM colorization as fallback — the latter can be more
/// saturated than the visible accent. Callers cache per process: a runtime accent
/// change is picked up on the next app start, like the rest of the palette.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
public static class SystemAccentColor
{
    public static bool TryGet(out Color color)
    {
        // HKCU\Software\Microsoft\Windows\DWM\AccentColor is ABGR.
        try
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                    "AccentColor", null) is int abgr)
            {
                color = Color.FromArgb(255, abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                return true;
            }
        }
        catch
        {
            // Fall through to the DWM API.
        }

        try
        {
            PInvoke.DwmGetColorizationColor(out uint colorization, out BOOL _).ThrowOnFailure();
            color = Color.FromArgb(255,
                (int)(colorization >> 16) & 0xFF,
                (int)(colorization >> 8) & 0xFF,
                (int)colorization & 0xFF);
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }
}
