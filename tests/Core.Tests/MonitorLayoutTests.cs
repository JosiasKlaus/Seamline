namespace Seamline.Core.Tests;

public class MonitorLayoutTests
{
    private static MonitorInfo Monitor(string device, int x, int y, int w = 1920, int h = 1080, bool primary = false)
        => new()
        {
            DeviceName = device,
            ScreenWidth = w,
            ScreenHeight = h,
            PositionX = x,
            PositionY = y,
            IsPrimary = primary,
        };

    [Fact]
    public void Duplicate_device_name_add_throws()
    {
        var layout = new MonitorLayout();
        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0));

        Assert.Throws<ArgumentException>(() => layout.Add(Monitor(@"\\.\DISPLAY1", 100, 100)));
    }

    [Fact]
    public void Add_raises_MonitorAdded_and_LayoutChanged()
    {
        var layout = new MonitorLayout();
        MonitorInfo? added = null;
        int layoutChanged = 0;
        layout.MonitorAdded += m => added = m;
        layout.LayoutChanged += () => layoutChanged++;

        var monitor = Monitor(@"\\.\DISPLAY1", 0, 0);
        layout.Add(monitor);

        Assert.Same(monitor, added);
        Assert.Equal(1, layoutChanged);
    }

    [Fact]
    public void Remove_raises_MonitorRemoved_and_LayoutChanged_unknown_raises_nothing()
    {
        var layout = new MonitorLayout();
        var monitor = Monitor(@"\\.\DISPLAY1", 0, 0);
        layout.Add(monitor);
        MonitorInfo? removed = null;
        int layoutChanged = 0;
        layout.MonitorRemoved += m => removed = m;
        layout.LayoutChanged += () => layoutChanged++;

        Assert.True(layout.Remove(monitor.DeviceName));
        Assert.Same(monitor, removed);
        Assert.Equal(1, layoutChanged);

        removed = null;
        Assert.False(layout.Remove(@"\\.\DISPLAY9"));
        Assert.Null(removed);
        Assert.Equal(1, layoutChanged);
    }

    [Fact]
    public void MonitorsInOrder_sorts_by_position_x_then_y()
    {
        var layout = new MonitorLayout();
        layout.Add(Monitor(@"\\.\DISPLAY3", 1920, -1080));
        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0, primary: true));
        layout.Add(Monitor(@"\\.\DISPLAY2", 0, -1080));

        Assert.Equal(
            [@"\\.\DISPLAY2", @"\\.\DISPLAY1", @"\\.\DISPLAY3"],
            layout.MonitorsInOrder.Select(m => m.DeviceName));
    }

    [Fact]
    public void PrimaryMonitor_returns_the_primary_or_null()
    {
        var layout = new MonitorLayout();
        Assert.Null(layout.PrimaryMonitor);

        layout.Add(Monitor(@"\\.\DISPLAY2", 0, -1080));
        Assert.Null(layout.PrimaryMonitor);

        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0, primary: true));
        Assert.Equal(@"\\.\DISPLAY1", layout.PrimaryMonitor?.DeviceName);
    }

    [Theory]
    [InlineData(0, 0)]        // top-left corner
    [InlineData(1920, 0)]     // top-right corner, inclusive right edge
    [InlineData(0, 1080)]     // bottom-left corner, inclusive bottom edge
    [InlineData(1920, 1080)]  // bottom-right corner, both inclusive
    public void Hit_test_edges_are_inclusive_on_all_four_sides(int x, int y)
    {
        var layout = new MonitorLayout();
        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0));

        Assert.True(layout.TryHitTest(x, y, out MonitorInfo? hit));
        Assert.Equal(@"\\.\DISPLAY1", hit!.DeviceName);
    }

    [Fact]
    public void Hit_test_on_shared_edge_resolves_to_first_in_insertion_order()
    {
        var layout = new MonitorLayout();
        layout.Add(Monitor(@"\\.\DISPLAY2", 0, -1080)); // bottom edge y=0 shared with DISPLAY1 top
        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0, primary: true));

        Assert.True(layout.TryHitTest(100, 0, out MonitorInfo? hit));
        Assert.Equal(@"\\.\DISPLAY2", hit!.DeviceName);
    }

    [Fact]
    public void Hit_test_misses_outside_and_between_monitors()
    {
        var layout = new MonitorLayout();
        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0));
        layout.Add(Monitor(@"\\.\DISPLAY2", 2000, 0)); // 80 px gap after DISPLAY1's inclusive right edge

        Assert.False(layout.TryHitTest(-1, 0, out _));
        Assert.False(layout.TryHitTest(0, 1081, out _));
        Assert.False(layout.TryHitTest(1960, 100, out _)); // in the gap
        Assert.True(layout.TryHitTest(1920, 100, out MonitorInfo? left));
        Assert.Equal(@"\\.\DISPLAY1", left!.DeviceName);
        Assert.True(layout.TryHitTest(2000, 100, out MonitorInfo? right));
        Assert.Equal(@"\\.\DISPLAY2", right!.DeviceName);
    }

    [Fact]
    public void Member_property_change_raises_MonitorChanged_not_LayoutChanged()
    {
        var layout = new MonitorLayout();
        var monitor = Monitor(@"\\.\DISPLAY2", 0, -1080);
        layout.Add(monitor);
        MonitorInfo? changed = null;
        int layoutChanged = 0;
        layout.MonitorChanged += m => changed = m;
        layout.LayoutChanged += () => layoutChanged++;

        monitor.PositionY = -1070;

        Assert.Same(monitor, changed);
        Assert.Equal(0, layoutChanged);
    }

    [Fact]
    public void Removed_member_no_longer_raises_MonitorChanged()
    {
        var layout = new MonitorLayout();
        var monitor = Monitor(@"\\.\DISPLAY2", 0, -1080);
        layout.Add(monitor);
        layout.Remove(monitor.DeviceName);
        int raised = 0;
        layout.MonitorChanged += _ => raised++;

        monitor.PositionY = -1070;

        Assert.Equal(0, raised);
    }

    [Fact]
    public void NotifyLayoutChanged_raises_LayoutChanged()
    {
        var layout = new MonitorLayout();
        int raised = 0;
        layout.LayoutChanged += () => raised++;

        layout.NotifyLayoutChanged();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ResetTo_replaces_population_and_raises_Reset()
    {
        var layout = new MonitorLayout();
        layout.Add(Monitor(@"\\.\DISPLAY1", 0, 0));
        bool reset = false;
        layout.Reset += () => reset = true;

        layout.ResetTo([Monitor(@"\\.\DISPLAY2", 0, -1080), Monitor(@"\\.\DISPLAY3", 1920, -1080)]);

        Assert.True(reset);
        Assert.Equal(2, layout.Count);
        Assert.False(layout.TryGet(@"\\.\DISPLAY1", out _));
    }

    [Fact]
    public void ResetTo_with_duplicate_throws_and_leaves_layout_untouched()
    {
        var layout = new MonitorLayout();
        var original = Monitor(@"\\.\DISPLAY1", 0, 0);
        layout.Add(original);
        int events = 0;
        layout.Reset += () => events++;
        layout.LayoutChanged += () => events++;

        Assert.Throws<ArgumentException>(() => layout.ResetTo(
            [Monitor(@"\\.\DISPLAY2", 0, 0), Monitor(@"\\.\DISPLAY2", 100, 0)]));

        Assert.Equal(0, events);
        Assert.Equal(1, layout.Count);
        Assert.True(layout.TryGet(@"\\.\DISPLAY1", out MonitorInfo? kept));
        Assert.Same(original, kept);

        // The old member is still subscribed after the failed reset.
        MonitorInfo? changed = null;
        layout.MonitorChanged += m => changed = m;
        original.PositionY = 5;
        Assert.Same(original, changed);
    }
}
