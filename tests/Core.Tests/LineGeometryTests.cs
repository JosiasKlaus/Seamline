namespace Seamline.Core.Tests;

public class LineGeometryTests
{
    [Fact]
    public void Segments_for_side_by_side_monitor_at_default_spacing()
    {
        // Primary 1920x1200 → mid=600, over=500, under=700; monitor at PositionY=0.
        var segments = LineGeometry.HorizontalSegments(
            primaryHeight: 1200, lineSpacing: 100, monitorPositionY: 0, screenWidth: 1920);

        Assert.Equal(
        [
            new LineSegment(0, 600, 1920, 600),      // mid, full width
            new LineSegment(0, 500, 384, 500),       // over-left stub (1920/5)
            new LineSegment(1536, 500, 1920, 500),   // over-right stub
            new LineSegment(0, 700, 384, 700),       // under-left stub
            new LineSegment(1536, 700, 1920, 700),   // under-right stub
        ], segments);
    }

    [Fact]
    public void Raising_position_y_moves_lines_up_on_screen()
    {
        // The ⚠ sign convention: Up key increases PositionY, local y = ref − PositionY
        // decreases, so the drawn lines move UP.
        var atZero = LineGeometry.HorizontalSegments(1200, 100, 0, 1920);
        var raised = LineGeometry.HorizontalSegments(1200, 100, 3, 1920);

        Assert.Equal(atZero[0].Y1 - 3, raised[0].Y1);
        Assert.Equal(atZero[1].Y1 - 3, raised[1].Y1);
        Assert.Equal(atZero[3].Y1 - 3, raised[3].Y1);
    }

    [Fact]
    public void References_track_the_primary_height_and_spacing()
    {
        var segments = LineGeometry.HorizontalSegments(1080, 250, 0, 1000);

        Assert.Equal(540, segments[0].Y1); // mid = 1080/2
        Assert.Equal(290, segments[1].Y1); // over = 540 − 250
        Assert.Equal(790, segments[3].Y1); // under = 540 + 250
        Assert.Equal(200, segments[1].X2); // stub = 1000/5
        Assert.Equal(800, segments[2].X1);
    }

    [Theory]
    [InlineData(0, true)]      // beside the primary: mid at 600 is visible
    [InlineData(550, true)]    // partially: mid at 50 still visible
    [InlineData(-1080, false)] // stacked above: all references land below the panel
    [InlineData(1300, false)]  // stacked below: all references land above the panel
    public void Visibility_reflects_whether_any_reference_line_lands_on_the_panel(int positionY, bool expected)
    {
        Assert.Equal(expected, LineGeometry.AnyHorizontalSegmentVisible(
            primaryHeight: 1200, lineSpacing: 100, monitorPositionY: positionY,
            screenWidth: 1920, screenHeight: 1080));
    }

    [Fact]
    public void Diagonals_span_corner_to_corner()
    {
        var segments = LineGeometry.DiagonalSegments(1920, 1080);

        Assert.Equal(
        [
            new LineSegment(0, 0, 1920, 1080),
            new LineSegment(1920, 0, 0, 1080),
        ], segments);
    }
}
