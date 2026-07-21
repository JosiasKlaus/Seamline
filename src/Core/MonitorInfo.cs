namespace Seamline.Core;

/// <summary>
/// One attached display. Positions are physical-pixel virtual-screen coordinates:
/// the primary monitor's top-left corner is (0,0) and negative values are valid.
/// </summary>
public sealed class MonitorInfo : ObservableObject
{
    private string _monitorName = "";
    private string _friendlyMonitorName = "";
    private string _displayAdapter = "";
    private int _screenWidth;
    private int _screenHeight;
    private int _positionX;
    private int _positionY;
    private MonitorOrientation _orientation;
    private bool _isPrimary;

    /// <summary>Win32 adapter device name (e.g. \\.\DISPLAY1) — the repositioning identity.</summary>
    public required string DeviceName { get; init; }

    public string MonitorName { get => _monitorName; set => Set(ref _monitorName, value); }
    public string FriendlyMonitorName { get => _friendlyMonitorName; set => Set(ref _friendlyMonitorName, value); }
    public string DisplayAdapter { get => _displayAdapter; set => Set(ref _displayAdapter, value); }
    public int ScreenWidth { get => _screenWidth; set => Set(ref _screenWidth, value); }
    public int ScreenHeight { get => _screenHeight; set => Set(ref _screenHeight, value); }
    public int PositionX { get => _positionX; set => Set(ref _positionX, value); }
    public int PositionY { get => _positionY; set => Set(ref _positionY, value); }
    public MonitorOrientation Orientation { get => _orientation; set => Set(ref _orientation, value); }
    public bool IsPrimary { get => _isPrimary; set => Set(ref _isPrimary, value); }

    public MonitorSnapshot CreateSnapshot() => new(
        DeviceName, MonitorName, FriendlyMonitorName, DisplayAdapter,
        ScreenWidth, ScreenHeight, PositionX, PositionY, Orientation, IsPrimary);

    /// <summary>
    /// Restores all mutable state from a snapshot, going through the property setters
    /// so PropertyChanged fires and the UI repaints — this is what makes rollback
    /// visually snap back.
    /// </summary>
    public void Restore(in MonitorSnapshot snapshot)
    {
        if (snapshot.DeviceName != DeviceName)
            throw new ArgumentException(
                $"Snapshot of '{snapshot.DeviceName}' cannot be restored onto '{DeviceName}'.", nameof(snapshot));

        MonitorName = snapshot.MonitorName;
        FriendlyMonitorName = snapshot.FriendlyMonitorName;
        DisplayAdapter = snapshot.DisplayAdapter;
        ScreenWidth = snapshot.ScreenWidth;
        ScreenHeight = snapshot.ScreenHeight;
        PositionX = snapshot.PositionX;
        PositionY = snapshot.PositionY;
        Orientation = snapshot.Orientation;
        IsPrimary = snapshot.IsPrimary;
    }
}
