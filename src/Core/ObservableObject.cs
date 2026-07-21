using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Seamline.Core;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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

    /// <summary>Setting a property to its current value is a silent no-op
    /// (equality suppression — the no-op/no-loop guarantee).</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed class Unsubscriber(ObservableObject owner, PropertyChangedEventHandler handler) : IDisposable
    {
        public void Dispose() => owner.PropertyChanged -= handler;
    }
}
