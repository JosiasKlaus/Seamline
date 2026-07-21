namespace Seamline.Core;

/// <summary>
/// Resolves which monitor anchors the alignment lines for a selected screen.
/// The reference must be a monitor that stays STILL while the selected one moves,
/// so the lines keep the original's feedback (they move on the screen being
/// adjusted, not on its neighbor). The primary is preferred whenever it shares
/// the selected screen's row/column — which reproduces the original app exactly
/// for rows — otherwise the nearest neighbor anchors; with no neighbor at all the
/// primary is the fallback (the overlay then shows the out-of-range hint).
/// Rows (vertical overlap) anchor the horizontal lines; columns (horizontal
/// overlap) anchor the vertical lines used for stacked layouts.
/// </summary>
public static class AlignmentReference
{
    /// <summary>Reference for the horizontal lines: a monitor in the selected
    /// screen's row (primary preferred, else nearest by horizontal gap).</summary>
    public static MonitorInfo Resolve(MonitorInfo selected, IReadOnlyList<MonitorInfo> monitors)
        => ResolveCore(selected, monitors, OverlapsVertically, HorizontalGap);

    /// <summary>Reference for the vertical lines: a monitor in the selected
    /// screen's column (primary preferred, else nearest by vertical gap).</summary>
    public static MonitorInfo ResolveColumn(MonitorInfo selected, IReadOnlyList<MonitorInfo> monitors)
        => ResolveCore(selected, monitors, OverlapsHorizontally, VerticalGap);

    /// <summary>The reference's horizontal center line in virtual-screen coordinates.
    /// For the primary this equals primaryHeight/2 — the original's formula.</summary>
    public static int CenterY(MonitorInfo monitor) => monitor.PositionY + monitor.ScreenHeight / 2;

    /// <summary>The reference's vertical center line in virtual-screen coordinates.</summary>
    public static int CenterX(MonitorInfo monitor) => monitor.PositionX + monitor.ScreenWidth / 2;

    private static MonitorInfo ResolveCore(
        MonitorInfo selected,
        IReadOnlyList<MonitorInfo> monitors,
        Func<MonitorInfo, MonitorInfo, bool> sharesAxis,
        Func<MonitorInfo, MonitorInfo, int> gap)
    {
        MonitorInfo primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? selected;
        if (selected.IsPrimary)
            return selected;

        var neighbors = monitors
            .Where(m => !ReferenceEquals(m, selected) && sharesAxis(m, selected))
            .ToList();
        if (neighbors.Count == 0)
            return primary;

        return neighbors.FirstOrDefault(m => m.IsPrimary)
            ?? neighbors.OrderBy(m => gap(m, selected)).First();
    }

    // Strict overlap: monitors that merely touch at an edge are not in one row/column.
    private static bool OverlapsVertically(MonitorInfo a, MonitorInfo b)
        => a.PositionY < b.PositionY + b.ScreenHeight && b.PositionY < a.PositionY + a.ScreenHeight;

    private static bool OverlapsHorizontally(MonitorInfo a, MonitorInfo b)
        => a.PositionX < b.PositionX + b.ScreenWidth && b.PositionX < a.PositionX + a.ScreenWidth;

    private static int HorizontalGap(MonitorInfo a, MonitorInfo b)
    {
        if (a.PositionX + a.ScreenWidth <= b.PositionX)
            return b.PositionX - (a.PositionX + a.ScreenWidth);
        if (b.PositionX + b.ScreenWidth <= a.PositionX)
            return a.PositionX - (b.PositionX + b.ScreenWidth);
        return 0;
    }

    private static int VerticalGap(MonitorInfo a, MonitorInfo b)
    {
        if (a.PositionY + a.ScreenHeight <= b.PositionY)
            return b.PositionY - (a.PositionY + a.ScreenHeight);
        if (b.PositionY + b.ScreenHeight <= a.PositionY)
            return a.PositionY - (b.PositionY + b.ScreenHeight);
        return 0;
    }
}
