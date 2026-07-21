namespace Seamline.Core;

public readonly record struct LineSegment(int X1, int Y1, int X2, int Y2);

/// <summary>
/// Alignment-line math, in physical pixels, in each overlay's local coordinates.
/// Reference heights come from the PRIMARY monitor: mid = primaryHeight/2,
/// over = mid − spacing, under = mid + spacing; a monitor's local y is
/// reference − PositionY. Up (PositionY +1) therefore moves the lines up on screen.
/// </summary>
public static class LineGeometry
{
    /// <summary>The mid line spans the full width; the over/under lines are stubs of
    /// width/5 anchored at the left and right edges (4 stubs). Order: mid,
    /// over-left, over-right, under-left, under-right.</summary>
    public static IReadOnlyList<LineSegment> HorizontalSegments(
        int primaryHeight, int lineSpacing, int monitorPositionY, int screenWidth)
    {
        int mid = primaryHeight / 2;
        int yMid = mid - monitorPositionY;
        int yOver = mid - lineSpacing - monitorPositionY;
        int yUnder = mid + lineSpacing - monitorPositionY;
        int stub = screenWidth / 5;
        return
        [
            new LineSegment(0, yMid, screenWidth, yMid),
            new LineSegment(0, yOver, stub, yOver),
            new LineSegment(screenWidth - stub, yOver, screenWidth, yOver),
            new LineSegment(0, yUnder, stub, yUnder),
            new LineSegment(screenWidth - stub, yUnder, screenWidth, yUnder),
        ];
    }

    /// <summary>Two corner-to-corner diagonals.</summary>
    public static IReadOnlyList<LineSegment> DiagonalSegments(int width, int height) =>
    [
        new LineSegment(0, 0, width, height),
        new LineSegment(width, 0, 0, height),
    ];

    /// <summary>False when every horizontal reference line falls outside the monitor's
    /// visible height — the case for monitors stacked above/below the primary, whose
    /// vertical offset horizontal lines cannot align.</summary>
    public static bool AnyHorizontalSegmentVisible(
        int primaryHeight, int lineSpacing, int monitorPositionY, int screenWidth, int screenHeight)
        => HorizontalSegments(primaryHeight, lineSpacing, monitorPositionY, screenWidth)
            .Any(s => s.Y1 >= 0 && s.Y1 < screenHeight);
}
