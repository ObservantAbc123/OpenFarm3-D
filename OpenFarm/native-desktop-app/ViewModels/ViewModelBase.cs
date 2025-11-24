// Purpose
//   Minimal MVVM base class providing property change notification helpers.
//   Implements INotifyPropertyChanged and exposes SetProperty<T>() to reduce
//   boilerplate in derived view models.
//
// Notes
//   • [CallerMemberName] captures the caller's property name automatically.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DatabaseAccess;
using RabbitMQHelper;

namespace native_desktop_app.ViewModels;

/// <summary>
///     Base class for view models implementing <see cref="INotifyPropertyChanged" />.
///     <para>
///         • Raises <see cref="PropertyChanged" /> when bound properties change.
///         • Provides <see cref="SetProperty{T}(ref T, T, string)" /> to simplify
///         property setters by handling equality checks and event notification.
///     </para>
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected readonly DatabaseAccessHelper _databaseAccessHelper;
    protected readonly IRmqHelper _rmqHelper;

    protected ViewModelBase(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper)
    {
        _databaseAccessHelper = databaseAccessHelper;
        _rmqHelper = rmqHelper;
    }

    /// <summary>
    ///     Occurs when a property value changes. UI frameworks listen to this event
    ///     to update bindings automatically.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    ///     Raises the <see cref="PropertyChanged" /> event for the specified property.
    ///     The property name is captured automatically by the compiler via
    ///     <see cref="CallerMemberNameAttribute" /> when not explicitly provided.
    /// </summary>
    /// <param name="propertyName">The name of the changed property.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Sets a backing field to the provided <paramref name="value" /> and raises
    ///     <see cref="PropertyChanged" /> if the value differs from the current field.
    ///     Intended for use within property setters in derived classes.
    /// </summary>
    /// <typeparam name="T">The type of the property/field.</typeparam>
    /// <param name="field">Reference to the backing field to update.</param>
    /// <param name="value">The new value to assign.</param>
    /// <param name="propertyName">The property name (filled automatically).</param>
    /// <returns><c>true</c> if the value changed and the event was raised; otherwise <c>false</c>.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}