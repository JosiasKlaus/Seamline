namespace Seamline.Core.Tests;

public class SettingsInitializerTests
{
    [Fact]
    public void Initializer_runs_before_SessionStarted_observers_see_settings()
    {
        var layout = new MonitorLayout();
        layout.Add(new MonitorInfo
        {
            DeviceName = @"\\.\DISPLAY1", ScreenWidth = 1920, ScreenHeight = 1200, IsPrimary = true,
        });
        var controller = new AlignController(new FakeBackend(), layout)
        {
            SettingsInitializer = s => s.LineSpacing = 250,
        };
        int observedAtStart = 0;
        controller.SessionStarted += () => observedAtStart = controller.Settings!.LineSpacing;

        controller.StartSession();

        Assert.Equal(250, observedAtStart); // persisted values visible before SessionStarted
        Assert.Equal(250, controller.Settings!.LineSpacing);
    }

    [Fact]
    public void Second_session_reinitializes_fresh_settings()
    {
        var layout = new MonitorLayout();
        layout.Add(new MonitorInfo
        {
            DeviceName = @"\\.\DISPLAY1", ScreenWidth = 1920, ScreenHeight = 1200, IsPrimary = true,
        });
        int calls = 0;
        var controller = new AlignController(new FakeBackend(), layout)
        {
            SettingsInitializer = _ => calls++,
        };

        controller.StartSession();
        controller.AbortSession();
        controller.StartSession();

        Assert.Equal(2, calls);
    }
}
