namespace Seamline.Core;

/// <summary>
/// Resolves which monitor anchors the alignment lines for a selected screen.
/// The reference must be a monitor that stays STILL while the selected one moves,
/// so the lines keep the original's feedback (they move on the screen being
/// adjusted, not on its neighbor). The primary is preferred whenever it shares
/// the selected screen's row — which reproduces the original app exactly —
/// otherwise the nearest row neighbor anchors; with no row neighbor at all the
/// primary is the fallback (the overlay then shows the out-of-range hint).
/// </summary>
public static class AlignmentReference
{
    public static MonitorInfo Resolve(MonitorInfo selected, IReadOnlyList<MonitorInfo> monitors)
    {
        MonitorInfo primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? selected;
        if (selected.IsPrimary)
            return selected;

        var row = monitors
            .Where(m => !ReferenceEquals(m, selected) && OverlapsVertically(m, selected))
            .ToList();
        if (row.Count == 0)
            return primary;

        return row.FirstOrDefault(m => m.IsPrimary)
            ?? row.OrderBy(m => HorizontalGap(m, selected)).First();
    }

    /// <summary>The reference's center line in virtual-screen coordinates. For the
    /// primary this equals primaryHeight/2 — the original's formula.</summary>
    public static int CenterY(MonitorInfo monitor) => monitor.PositionY + monitor.ScreenHeight / 2;

    // Strict overlap: monitors that merely touch at an edge are not in one row.
    private static bool OverlapsVertically(MonitorInfo a, MonitorInfo b)
        => a.PositionY < b.PositionY + b.ScreenHeight && b.PositionY < a.PositionY + a.ScreenHeight;

    private static int HorizontalGap(MonitorInfo a, MonitorInfo b)
    {
        if (a.PositionX + a.ScreenWidth <= b.PositionX)
            return b.PositionX - (a.PositionX + a.ScreenWidth);
        if (b.PositionX + b.ScreenWidth <= a.PositionX)
            return a.PositionX - (b.PositionX + b.ScreenWidth);
        return 0;
    }
}
