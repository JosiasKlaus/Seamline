namespace Seamline.Core.Tests;

public class VerticalAlignmentTests
{
    private static MonitorInfo Monitor(string device, int x, int y, int w = 1920, int h = 1080, bool primary = false)
        => new()
        {
            DeviceName = device, ScreenWidth = w, ScreenHeight = h,
            PositionX = x, PositionY = y, IsPrimary = primary,
        };

    [Fact]
    public void Vertical_segments_mirror_the_horizontal_structure()
    {
        // Reference column center 960, spacing 100, monitor at PositionX=0, height 1080.
        var segments = LineGeometry.VerticalSegments(
            referenceCenterX: 960, lineSpacing: 100, monitorPositionX: 0, screenHeight: 1080);

        Assert.Equal(
        [
            new LineSegment(960, 0, 960, 1080),      // mid, full height
            new LineSegment(860, 0, 860, 216),       // left-top stub (1080/5)
            new LineSegment(860, 864, 860, 1080),    // left-bottom stub
            new LineSegment(1060, 0, 1060, 216),     // right-top stub
            new LineSegment(1060, 864, 1060, 1080),  // right-bottom stub
        ], segments);
    }

    [Fact]
    public void Decreasing_position_x_moves_lines_right_on_screen()
    {
        // The Right key decreases PositionX; local x = ref − PositionX increases,
        // so the lines move right — the line follows the key, like Up=lines-up.
        var atZero = LineGeometry.VerticalSegments(960, 100, 0, 1080);
        var moved = LineGeometry.VerticalSegments(960, 100, -3, 1080);

        Assert.Equal(atZero[0].X1 + 3, moved[0].X1);
    }

    [Theory]
    [InlineData(960, 0, true)]      // same column: mid at 960 visible
    [InlineData(960, 2000, false)]  // different column: all lines left of the panel
    [InlineData(-540, -1080, true)] // column overlap in negative coordinates
    public void Vertical_visibility_reflects_column_membership(int referenceCenterX, int positionX, bool expected)
    {
        Assert.Equal(expected, LineGeometry.AnyVerticalSegmentVisible(
            referenceCenterX, lineSpacing: 100, monitorPositionX: positionX,
            screenHeight: 1080, screenWidth: 1920));
    }

    [Fact]
    public void Column_reference_prefers_the_primary_in_the_selected_column()
    {
        // The user's stacked pair: DISPLAY2 directly above the primary.
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var above = Monitor("A", 0, -1080);
        var beside = Monitor("B", 1920, -1080);

        Assert.Same(primary, AlignmentReference.ResolveColumn(above, [primary, above, beside]));
        Assert.Equal(960, AlignmentReference.CenterX(primary));
    }

    [Fact]
    public void Column_without_primary_references_the_nearest_static_neighbor()
    {
        var primary = Monitor("P", 4000, 0, 1920, 1200, primary: true); // far away column
        var top = Monitor("T", 0, -1080);
        var bottom = Monitor("B", 0, 0);

        Assert.Same(bottom, AlignmentReference.ResolveColumn(top, [primary, top, bottom]));
        Assert.Same(top, AlignmentReference.ResolveColumn(bottom, [primary, top, bottom]));
    }

    [Fact]
    public void Monitor_with_no_column_neighbor_falls_back_to_the_primary()
    {
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var besideOnly = Monitor("S", 1920, 0); // shares a row, not a column

        Assert.Same(primary, AlignmentReference.ResolveColumn(besideOnly, [primary, besideOnly]));
    }

    [Fact]
    public void TryMove_applies_both_axes_and_refuses_primary()
    {
        var backend = new FakeBackend();
        var layout = new MonitorLayout();
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var above = Monitor("A", 0, -1080);
        layout.Add(primary);
        layout.Add(above);
        var controller = new AlignController(backend, layout);
        controller.StartSession();

        Assert.True(controller.TryMove("A", -3, 0));
        Assert.Equal(-3, above.PositionX);
        Assert.Equal(-1080, above.PositionY);
        Assert.True(controller.TryMove("A", 0, 2));
        Assert.Equal(-1078, above.PositionY);
        Assert.False(controller.TryMove("P", 1, 1));

        // The stage pass carries the edited X, and revert restores it.
        controller.Apply();
        Assert.Contains("stage A (-3, -1078)", backend.Calls);
        StageCommitOutcome reverted = controller.RevertApplied();
        Assert.True(reverted.Succeeded);
        Assert.Equal(0, above.PositionX);
        Assert.Equal(-1080, above.PositionY);
    }

    [Fact]
    public void Vertical_lines_default_off_and_notify()
    {
        var settings = new AlignSettings();
        Assert.False(settings.ShowVerticalLines);

        string? changed = null;
        settings.PropertyChanged += (_, e) => changed = e.PropertyName;
        settings.ShowVerticalLines = true;
        Assert.Equal(nameof(AlignSettings.ShowVerticalLines), changed);
    }
}
