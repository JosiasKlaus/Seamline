using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Seamline.Core;

/// <summary>
/// One attached display. Positions are physical-pixel virtual-screen coordinates:
/// the primary monitor's top-left corner is (0,0) and negative values are valid.
/// </summary>
public sealed class MonitorInfo : INotifyPropertyChanged
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

    public event PropertyChangedEventHandler? PropertyChanged;

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

    /// <summary>Subscribes to changes of a single property; dispose to unsubscribe.</summary>
    public IDisposable WhenChanged(string propertyName, Action handler)
    {
        PropertyChangedEventHandler subscription = (_, e) =>
        {
            if (e.PropertyName == propertyName)
                handler();
        };
        PropertyChanged += subscription;
        return new Unsubscriber(this, subscription);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class Unsubscriber(MonitorInfo owner, PropertyChangedEventHandler handler) : IDisposable
    {
        public void Dispose() => owner.PropertyChanged -= handler;
    }
}
