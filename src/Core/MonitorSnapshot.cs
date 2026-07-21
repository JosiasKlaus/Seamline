namespace Seamline.Core;

/// <summary>Immutable capture of a monitor's full state, used for session rollback.
/// Rollback is purely in-memory; only Apply touches the OS.</summary>
public readonly record struct MonitorSnapshot(
    string DeviceName,
    string MonitorName,
    string FriendlyMonitorName,
    string DisplayAdapter,
    int ScreenWidth,
    int ScreenHeight,
    int PositionX,
    int PositionY,
    MonitorOrientation Orientation,
    bool IsPrimary);
