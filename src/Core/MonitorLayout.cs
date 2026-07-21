using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Seamline.Core;

/// <summary>
/// The set of attached monitors, keyed by DeviceName, in insertion order.
/// Event contract (mirrors the original's semantics): LayoutChanged fires on
/// membership changes and — via NotifyLayoutChanged — after an OS-level apply;
/// pending in-memory edits of a member raise MonitorChanged only. Overlay and
/// overview geometry must bind to LayoutChanged, never to MonitorChanged.
/// </summary>
public sealed class MonitorLayout
{
    private readonly List<MonitorInfo> _monitors = [];
    private readonly Dictionary<string, MonitorInfo> _byDevice = new();
    private readonly Dictionary<string, PropertyChangedEventHandler> _memberSubscriptions = new();

    public event Action<MonitorInfo>? MonitorAdded;
    public event Action<MonitorInfo>? MonitorRemoved;
    public event Action? Reset;
    public event Action? LayoutChanged;

    /// <summary>A member monitor's property changed in memory (pending edit).</summary>
    public event Action<MonitorInfo>? MonitorChanged;

    /// <summary>Monitors in insertion order.</summary>
    public IReadOnlyList<MonitorInfo> Monitors => _monitors;

    public int Count => _monitors.Count;

    public MonitorInfo? PrimaryMonitor => _monitors.FirstOrDefault(m => m.IsPrimary);

    /// <summary>Monitors sorted by (PositionX, PositionY).</summary>
    public IReadOnlyList<MonitorInfo> MonitorsInOrder =>
        _monitors.OrderBy(m => m.PositionX).ThenBy(m => m.PositionY).ToList();

    public void Add(MonitorInfo monitor)
    {
        if (!_byDevice.TryAdd(monitor.DeviceName, monitor))
            throw new ArgumentException($"A monitor with device name '{monitor.DeviceName}' is already present.",
                nameof(monitor));
        _monitors.Add(monitor);
        Subscribe(monitor);
        MonitorAdded?.Invoke(monitor);
        LayoutChanged?.Invoke();
    }

    public bool Remove(string deviceName)
    {
        if (!_byDevice.Remove(deviceName, out MonitorInfo? monitor))
            return false;
        _monitors.Remove(monitor);
        Unsubscribe(monitor);
        MonitorRemoved?.Invoke(monitor);
        LayoutChanged?.Invoke();
        return true;
    }

    /// <summary>Replaces the whole population (e.g. after re-enumeration).
    /// Commit-or-rollback: on a duplicate device name nothing is mutated and no event fires.</summary>
    public void ResetTo(IEnumerable<MonitorInfo> monitors)
    {
        var incoming = new List<MonitorInfo>();
        var incomingByDevice = new Dictionary<string, MonitorInfo>();
        foreach (MonitorInfo monitor in monitors)
        {
            if (!incomingByDevice.TryAdd(monitor.DeviceName, monitor))
                throw new ArgumentException($"Duplicate device name '{monitor.DeviceName}' in reset population.",
                    nameof(monitors));
            incoming.Add(monitor);
        }

        foreach (MonitorInfo existing in _monitors)
            Unsubscribe(existing);
        _monitors.Clear();
        _byDevice.Clear();
        foreach (MonitorInfo monitor in incoming)
        {
            _monitors.Add(monitor);
            _byDevice[monitor.DeviceName] = monitor;
            Subscribe(monitor);
        }

        Reset?.Invoke();
        LayoutChanged?.Invoke();
    }

    public bool TryGet(string deviceName, [NotNullWhen(true)] out MonitorInfo? monitor)
        => _byDevice.TryGetValue(deviceName, out monitor);

    /// <summary>
    /// Finds the monitor containing the point. Edges are inclusive on all four sides;
    /// a point on a shared edge resolves to the first monitor in insertion order.
    /// </summary>
    public bool TryHitTest(int x, int y, [NotNullWhen(true)] out MonitorInfo? monitor)
    {
        foreach (MonitorInfo m in _monitors)
        {
            if (x >= m.PositionX && x <= m.PositionX + m.ScreenWidth
                && y >= m.PositionY && y <= m.PositionY + m.ScreenHeight)
            {
                monitor = m;
                return true;
            }
        }
        monitor = null;
        return false;
    }

    /// <summary>Raised by callers after an OS-level apply so views refresh.</summary>
    public void NotifyLayoutChanged() => LayoutChanged?.Invoke();

    private void Subscribe(MonitorInfo monitor)
    {
        PropertyChangedEventHandler handler = (_, _) => MonitorChanged?.Invoke(monitor);
        _memberSubscriptions[monitor.DeviceName] = handler;
        monitor.PropertyChanged += handler;
    }

    private void Unsubscribe(MonitorInfo monitor)
    {
        if (_memberSubscriptions.Remove(monitor.DeviceName, out PropertyChangedEventHandler? handler))
            monitor.PropertyChanged -= handler;
    }
}
