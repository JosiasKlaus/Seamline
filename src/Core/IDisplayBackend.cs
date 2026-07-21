using System.Drawing;

namespace Seamline.Core;

/// <summary>
/// The single OS seam. Repositioning is a two-phase protocol: stage every monitor's
/// new position, then commit once — Windows applies all staged changes as one atomic
/// modeset. Never apply positions one-at-a-time; transient invalid layouts get rejected.
/// </summary>
public interface IDisplayBackend
{
    IReadOnlyList<MonitorInfo> EnumerateMonitors();

    /// <summary>Live OS query — never derived from the model.</summary>
    Rectangle GetVirtualScreenBounds();

    DisplayChangeResult StagePosition(string deviceName, int x, int y);

    DisplayChangeResult CommitStaged();
}
