namespace Seamline.Core.Tests;

public class AlignmentReferenceTests
{
    private static MonitorInfo Monitor(string device, int x, int y, int w = 1920, int h = 1080, bool primary = false)
        => new()
        {
            DeviceName = device, ScreenWidth = w, ScreenHeight = h,
            PositionX = x, PositionY = y, IsPrimary = primary,
        };

    [Fact]
    public void Primary_in_the_selected_row_is_preferred_reproducing_the_original()
    {
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var beside = Monitor("S", 1920, 60);
        var monitors = new List<MonitorInfo> { primary, beside };

        Assert.Same(primary, AlignmentReference.Resolve(beside, monitors));
        Assert.Equal(600, AlignmentReference.CenterY(primary)); // primaryHeight/2, original formula
    }

    [Fact]
    public void Selected_primary_references_itself()
    {
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var other = Monitor("S", 1920, 0);

        Assert.Same(primary, AlignmentReference.Resolve(primary, [primary, other]));
    }

    [Fact]
    public void Row_without_primary_references_the_nearest_static_neighbor()
    {
        // The user's desk: primary below, two monitors stacked above side by side.
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var left = Monitor("L", 0, -1080);
        var right = Monitor("R", 1920, -1080);
        var monitors = new List<MonitorInfo> { primary, left, right };

        Assert.Same(right, AlignmentReference.Resolve(left, monitors));
        Assert.Same(left, AlignmentReference.Resolve(right, monitors));
        Assert.Equal(-540, AlignmentReference.CenterY(right));
    }

    [Fact]
    public void Nearest_neighbor_wins_in_a_three_wide_row()
    {
        var primary = Monitor("P", 0, 1300, 1920, 1200, primary: true); // different row
        var a = Monitor("A", 0, 0);
        var b = Monitor("B", 1920, 0);
        var c = Monitor("C", 3840, 0);

        Assert.Same(b, AlignmentReference.Resolve(c, [primary, a, b, c]));
        Assert.Same(b, AlignmentReference.Resolve(a, [primary, a, b, c]));
    }

    [Fact]
    public void Monitor_with_no_row_neighbor_falls_back_to_the_primary()
    {
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var stackedAlone = Monitor("S", 0, -1080); // touches the primary's edge only

        Assert.Same(primary, AlignmentReference.Resolve(stackedAlone, [primary, stackedAlone]));
    }

    [Fact]
    public void Edge_touching_monitors_are_not_one_row()
    {
        // Strict overlap: y-ranges merely touching (0 and 1080-high above) do not
        // make a row — only genuine vertical overlap does.
        var primary = Monitor("P", 0, 0, 1920, 1200, primary: true);
        var above = Monitor("A", 0, -1080);
        var besideAbove = Monitor("B", 1920, -1080);

        // "above" overlaps "besideAbove" (identical range) but not the primary.
        Assert.Same(besideAbove, AlignmentReference.Resolve(above, [primary, above, besideAbove]));
    }
}
