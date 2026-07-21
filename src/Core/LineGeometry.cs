namespace Seamline.Core;

public readonly record struct LineSegment(int X1, int Y1, int X2, int Y2);

/// <summary>
/// Alignment-line math, in physical pixels, in each overlay's local coordinates.
/// Reference heights come from a reference monitor's virtual-screen center line
/// (see AlignmentReference): mid = referenceCenterY, over = mid − spacing,
/// under = mid + spacing; a monitor's local y is reference − PositionY.
/// Up (PositionY +1) therefore moves the lines up on screen. With the primary as
/// reference, referenceCenterY = primaryHeight/2 — the original's exact formula.
/// </summary>
public static class LineGeometry
{
    /// <summary>The mid line spans the full width; the over/under lines are stubs of
    /// width/5 anchored at the left and right edges (4 stubs). Order: mid,
    /// over-left, over-right, under-left, under-right.</summary>
    public static IReadOnlyList<LineSegment> HorizontalSegments(
        int referenceCenterY, int lineSpacing, int monitorPositionY, int screenWidth)
    {
        int yMid = referenceCenterY - monitorPositionY;
        int yOver = referenceCenterY - lineSpacing - monitorPositionY;
        int yUnder = referenceCenterY + lineSpacing - monitorPositionY;
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

    /// <summary>
    /// Vertical alignment lines for stacked layouts, mirroring the horizontal set:
    /// mid column spans the full height; the left/right columns (± spacing) are stubs
    /// of height/5 anchored at the top and bottom edges. Local x = referenceCenterX −
    /// PositionX, so increasing PositionX moves the lines left on screen — the UI maps
    /// Left=+1 so the lines follow the key, mirroring the horizontal set's Up=+1.
    /// Order: mid, left-top, left-bottom, right-top, right-bottom.
    /// </summary>
    public static IReadOnlyList<LineSegment> VerticalSegments(
        int referenceCenterX, int lineSpacing, int monitorPositionX, int screenHeight)
    {
        int xMid = referenceCenterX - monitorPositionX;
        int xLeft = referenceCenterX - lineSpacing - monitorPositionX;
        int xRight = referenceCenterX + lineSpacing - monitorPositionX;
        int stub = screenHeight / 5;
        return
        [
            new LineSegment(xMid, 0, xMid, screenHeight),
            new LineSegment(xLeft, 0, xLeft, stub),
            new LineSegment(xLeft, screenHeight - stub, xLeft, screenHeight),
            new LineSegment(xRight, 0, xRight, stub),
            new LineSegment(xRight, screenHeight - stub, xRight, screenHeight),
        ];
    }

    /// <summary>False when every vertical reference line falls outside the monitor's
    /// visible width — the case for monitors in a different column than the reference.</summary>
    public static bool AnyVerticalSegmentVisible(
        int referenceCenterX, int lineSpacing, int monitorPositionX, int screenHeight, int screenWidth)
        => VerticalSegments(referenceCenterX, lineSpacing, monitorPositionX, screenHeight)
            .Any(s => s.X1 >= 0 && s.X1 < screenWidth);

    /// <summary>Two corner-to-corner diagonals.</summary>
    public static IReadOnlyList<LineSegment> DiagonalSegments(int width, int height) =>
    [
        new LineSegment(0, 0, width, height),
        new LineSegment(width, 0, 0, height),
    ];

    /// <summary>False when every horizontal reference line falls outside the monitor's
    /// visible height — the case for monitors in a different row than the reference.</summary>
    public static bool AnyHorizontalSegmentVisible(
        int referenceCenterY, int lineSpacing, int monitorPositionY, int screenWidth, int screenHeight)
        => HorizontalSegments(referenceCenterY, lineSpacing, monitorPositionY, screenWidth)
            .Any(s => s.Y1 >= 0 && s.Y1 < screenHeight);
}
