namespace Seamline.Core;

/// <summary>
/// Display orientation. Numeric values must stay identical to the Win32
/// DEVMODE dmDisplayOrientation constants DMDO_DEFAULT/DMDO_90/DMDO_180/DMDO_270.
/// </summary>
public enum MonitorOrientation
{
    Landscape = 0,
    Portrait = 1,
    FlippedLandscape = 2,
    FlippedPortrait = 3,
}
