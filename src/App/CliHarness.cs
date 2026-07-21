using System.Runtime.Versioning;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// Hidden dev/test modes (--dump, --move). Output goes to stdout, which is silent for
/// a double-clicked WinExe but captured when run from a script with redirection.
/// --dump output is the first thing to request in any bug report.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal static class CliHarness
{
    public static int Dump(IDisplayBackend backend)
    {
        var vs = backend.GetVirtualScreenBounds();
        Console.WriteLine($"virtual screen: pos=({vs.X}, {vs.Y}) size={vs.Width}x{vs.Height}");
        foreach (var m in backend.EnumerateMonitors())
        {
            Console.WriteLine(
                $"{m.DeviceName} pos=({m.PositionX}, {m.PositionY}) size={m.ScreenWidth}x{m.ScreenHeight} " +
                $"orientation={m.Orientation} primary={m.IsPrimary}");
            Console.WriteLine(
                $"  monitor='{m.MonitorName}' friendly='{m.FriendlyMonitorName}' adapter='{m.DisplayAdapter}'");
        }
        return 0;
    }

    public static int Move(IDisplayBackend backend, string deviceName, int dx, int dy)
    {
        var target = backend.EnumerateMonitors()
            .FirstOrDefault(m => m.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            Console.WriteLine($"device not found: {deviceName}");
            return 3;
        }
        if (target.IsPrimary)
        {
            Console.WriteLine("refusing to move the primary monitor");
            return 4;
        }

        int origX = target.PositionX, origY = target.PositionY;
        Console.WriteLine($"moving {deviceName} from ({origX}, {origY}) by ({dx}, {dy})");

        var stage = backend.StagePosition(deviceName, origX + dx, origY + dy);
        Console.WriteLine($"stage:         {stage}");
        if (!stage.Succeeded)
            return 5;
        var commit = backend.CommitStaged();
        Console.WriteLine($"commit:        {commit}");
        if (!commit.Succeeded)
        {
            // The stage already wrote the registry; leave no residue behind.
            Console.WriteLine("commit failed — reverting registry");
            Console.WriteLine($"revert stage:  {backend.StagePosition(deviceName, origX, origY)}");
            Console.WriteLine($"revert commit: {backend.CommitStaged()}");
            return 5;
        }

        var moved = backend.EnumerateMonitors().First(m => m.DeviceName == target.DeviceName);
        Console.WriteLine($"now at:        ({moved.PositionX}, {moved.PositionY})");

        var stageBack = backend.StagePosition(deviceName, origX, origY);
        Console.WriteLine($"stage revert:  {stageBack}");
        var commitBack = backend.CommitStaged();
        Console.WriteLine($"commit revert: {commitBack}");
        if (!stageBack.Succeeded || !commitBack.Succeeded)
            return 6;

        var restored = backend.EnumerateMonitors().First(m => m.DeviceName == target.DeviceName);
        Console.WriteLine($"restored to:   ({restored.PositionX}, {restored.PositionY})");

        bool ok = restored.PositionX == origX && restored.PositionY == origY
               && moved.PositionX == origX + dx && moved.PositionY == origY + dy;
        Console.WriteLine(ok ? "MOVE/REVERT OK" : "MOVE/REVERT MISMATCH");
        return ok ? 0 : 7;
    }
}
