using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Frequify.Infrastructure;

/// <summary>
/// Provides base property notification support for MVVM view models and bindable models.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>
    /// Raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Assigns a field and raises <see cref="PropertyChanged"/> when the value has changed.
    /// </summary>
    /// <typeparam name="T">Field type.</typeparam>
    /// <param name="field">Backing field.</param>
    /// <param name="value">New value.</param>
    /// <param name="propertyName">Property name inferred by compiler.</param>
    /// <returns>True when value changed; otherwise false.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises property changed for a specific property.
    /// </summary>
    /// <param name="propertyName">Property name inferred by compiler.</param>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
