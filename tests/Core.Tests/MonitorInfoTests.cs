namespace Seamline.Core.Tests;

public class MonitorInfoTests
{
    private static MonitorInfo Sample() => new()
    {
        DeviceName = @"\\.\DISPLAY1",
        MonitorName = "Generic PnP Monitor",
        FriendlyMonitorName = "EV2450",
        DisplayAdapter = "Intel(R) Graphics",
        ScreenWidth = 1920,
        ScreenHeight = 1080,
        PositionX = 0,
        PositionY = -1080,
        Orientation = MonitorOrientation.Landscape,
        IsPrimary = false,
    };

    [Fact]
    public void Setting_same_value_raises_no_event()
    {
        var monitor = Sample();
        int raised = 0;
        monitor.PropertyChanged += (_, _) => raised++;

        monitor.PositionY = -1080;
        monitor.MonitorName = "Generic PnP Monitor";

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Effective_change_raises_event_with_property_name()
    {
        var monitor = Sample();
        var names = new List<string?>();
        monitor.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        monitor.PositionY = -1070;
        monitor.IsPrimary = true;

        Assert.Equal([nameof(MonitorInfo.PositionY), nameof(MonitorInfo.IsPrimary)], names);
    }

    [Fact]
    public void WhenChanged_fires_only_for_named_property_and_stops_after_dispose()
    {
        var monitor = Sample();
        int fired = 0;
        IDisposable subscription = monitor.WhenChanged(nameof(MonitorInfo.PositionY), () => fired++);

        monitor.PositionX = 5;
        Assert.Equal(0, fired);
        monitor.PositionY = 5;
        Assert.Equal(1, fired);

        subscription.Dispose();
        monitor.PositionY = 6;
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Snapshot_restore_goes_through_setters_and_fires_events()
    {
        var monitor = Sample();
        MonitorSnapshot snapshot = monitor.CreateSnapshot();

        monitor.PositionY = 42;
        monitor.MonitorName = "changed";

        var names = new List<string?>();
        monitor.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        monitor.Restore(snapshot);

        Assert.Equal(-1080, monitor.PositionY);
        Assert.Equal("Generic PnP Monitor", monitor.MonitorName);
        Assert.Contains(nameof(MonitorInfo.PositionY), names);
        Assert.Contains(nameof(MonitorInfo.MonitorName), names);
    }

    [Fact]
    public void Restore_of_unchanged_state_is_silent()
    {
        var monitor = Sample();
        MonitorSnapshot snapshot = monitor.CreateSnapshot();
        int raised = 0;
        monitor.PropertyChanged += (_, _) => raised++;

        monitor.Restore(snapshot);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Restore_with_foreign_device_name_throws()
    {
        var monitor = Sample();
        var other = new MonitorInfo { DeviceName = @"\\.\DISPLAY9" };

        Assert.Throws<ArgumentException>(() => monitor.Restore(other.CreateSnapshot()));
    }
}
