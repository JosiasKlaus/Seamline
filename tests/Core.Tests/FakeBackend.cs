using System.Drawing;

namespace Seamline.Core.Tests;

/// <summary>Records every backend call in order; results are configurable per device.</summary>
public sealed class FakeBackend : IDisplayBackend
{
    private static readonly DisplayChangeResult Success = new(0, "DISP_CHANGE_SUCCESSFUL");

    public List<string> Calls { get; } = [];
    public List<MonitorInfo> Monitors { get; set; } = [];
    public Rectangle VirtualScreen { get; set; }
    public Dictionary<string, DisplayChangeResult> StageResultOverrides { get; } = new();
    public DisplayChangeResult CommitResult { get; set; } = Success;

    /// <summary>Stage calls for this device throw after being recorded (simulates an
    /// interop exception rather than a DISP_CHANGE error result).</summary>
    public string? ThrowOnStageDevice { get; set; }

    /// <summary>Runs inside CommitStaged after recording the call (simulates the
    /// synchronous WM_DISPLAYCHANGE dispatch of ChangeDisplaySettingsEx).</summary>
    public Action? BeforeCommit { get; set; }

    public IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        Calls.Add("enumerate");
        return Monitors;
    }

    public Rectangle GetVirtualScreenBounds()
    {
        Calls.Add("bounds");
        return VirtualScreen;
    }

    public DisplayChangeResult StagePosition(string deviceName, int x, int y)
    {
        Calls.Add($"stage {deviceName} ({x}, {y})");
        if (deviceName == ThrowOnStageDevice)
            throw new InvalidOperationException($"simulated interop failure staging {deviceName}");
        return StageResultOverrides.TryGetValue(deviceName, out DisplayChangeResult r) ? r : Success;
    }

    public DisplayChangeResult CommitStaged()
    {
        Calls.Add("commit");
        BeforeCommit?.Invoke();
        return CommitResult;
    }
}
