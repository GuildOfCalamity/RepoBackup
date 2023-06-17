#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WinUIDemo;

//
// Summary:
//     A base class for objects of which the properties must be observable.
public abstract class ObservableObject : INotifyPropertyChanged, INotifyPropertyChanging
{
    //
    // Summary:
    //     An interface for task notifiers of a specified type.
    //
    // Type parameters:
    //   TTask:
    //     The type of value to store.
    private interface ITaskNotifier<TTask> where TTask : Task
    {
        //
        // Summary:
        //     Gets or sets the wrapped TTask value.
        TTask? Task { get; set; }
    }

    //
    // Summary:
    //     A wrapping class that can hold a System.Threading.Tasks.Task value.
    protected sealed class TaskNotifier : ITaskNotifier<Task>
    {
        private Task? task;

        Task? ITaskNotifier<Task>.Task
        {
            get
            {
                return task;
            }
            set
            {
                task = value;
            }
        }

        //
        // Summary:
        //     Initializes a new instance of the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier
        //     class.
        internal TaskNotifier()
        {
        }

        //
        // Summary:
        //     Unwraps the System.Threading.Tasks.Task value stored in the current instance.
        //
        // Parameters:
        //   notifier:
        //     The input CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier`1
        //     instance.
        public static implicit operator Task?(TaskNotifier? notifier)
        {
            return notifier?.task;
        }
    }

    //
    // Summary:
    //     A wrapping class that can hold a System.Threading.Tasks.Task`1 value.
    //
    // Type parameters:
    //   T:
    //     The type of value for the wrapped System.Threading.Tasks.Task`1 instance.
    protected sealed class TaskNotifier<T> : ITaskNotifier<Task<T>>
    {
        private Task<T>? task;

        Task<T>? ITaskNotifier<Task<T>>.Task
        {
            get
            {
                return task;
            }
            set
            {
                task = value;
            }
        }

        //
        // Summary:
        //     Initializes a new instance of the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier`1
        //     class.
        internal TaskNotifier()
        {
        }

        //
        // Summary:
        //     Unwraps the System.Threading.Tasks.Task`1 value stored in the current instance.
        //
        // Parameters:
        //   notifier:
        //     The input CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier`1
        //     instance.
        public static implicit operator Task<T>?(TaskNotifier<T>? notifier)
        {
            return notifier?.task;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;

    //
    // Summary:
    //     Raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event.
    //
    // Parameters:
    //   e:
    //     The input System.ComponentModel.PropertyChangedEventArgs instance.
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        this.PropertyChanged?.Invoke(this, e);
    }

    //
    // Summary:
    //     Raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event.
    //
    // Parameters:
    //   e:
    //     The input System.ComponentModel.PropertyChangingEventArgs instance.
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        this.PropertyChanging?.Invoke(this, e);
    }

    //
    // Summary:
    //     Raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event.
    //
    // Parameters:
    //   propertyName:
    //     (optional) The name of the property that changed.
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    //
    // Summary:
    //     Raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event.
    //
    // Parameters:
    //   propertyName:
    //     (optional) The name of the property that changed.
    protected void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
    }

    //
    // Summary:
    //     Compares the current and new values for a given property. If the value has changed,
    //     raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the property with the new value, then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event.
    //
    // Parameters:
    //   field:
    //     The field storing the property's value.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   T:
    //     The type of the property that changed.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    protected bool SetProperty<T>([NotNullIfNotNull("newValue")] ref T field, T newValue, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        field = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }

    //
    // Summary:
    //     Compares the current and new values for a given property. If the value has changed,
    //     raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the property with the new value, then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. See additional notes about this overload in CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String).
    //
    // Parameters:
    //   field:
    //     The field storing the property's value.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   comparer:
    //     The System.Collections.Generic.IEqualityComparer`1 instance to use to compare
    //     the input values.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   T:
    //     The type of the property that changed.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    protected bool SetProperty<T>([NotNullIfNotNull("newValue")] ref T field, T newValue, IEqualityComparer<T> comparer, [CallerMemberName] string? propertyName = null)
    {
        if (comparer.Equals(field, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        field = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }

    //
    // Summary:
    //     Compares the current and new values for a given property. If the value has changed,
    //     raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the property with the new value, then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. This overload is much less efficient than CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String)
    //     and it should only be used when the former is not viable (eg. when the target
    //     property being updated does not directly expose a backing field that can be passed
    //     by reference). For performance reasons, it is recommended to use a stateful callback
    //     if possible through the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``2(``1,``1,``0,System.Action{``0,``1},System.String)
    //     whenever possible instead of this overload, as that will allow the C# compiler
    //     to cache the input callback and reduce the memory allocations. More info on that
    //     overload are available in the related XML docs. This overload is here for completeness
    //     and in cases where that is not applicable.
    //
    // Parameters:
    //   oldValue:
    //     The current property value.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   callback:
    //     A callback to invoke to update the property value.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   T:
    //     The type of the property that changed.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    protected bool SetProperty<T>(T oldValue, T newValue, Action<T> callback, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        callback(newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    //
    // Summary:
    //     Compares the current and new values for a given property. If the value has changed,
    //     raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the property with the new value, then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. See additional notes about this overload in CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0,``0,System.Action{``0},System.String).
    //
    // Parameters:
    //   oldValue:
    //     The current property value.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   comparer:
    //     The System.Collections.Generic.IEqualityComparer`1 instance to use to compare
    //     the input values.
    //
    //   callback:
    //     A callback to invoke to update the property value.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   T:
    //     The type of the property that changed.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    protected bool SetProperty<T>(T oldValue, T newValue, IEqualityComparer<T> comparer, Action<T> callback, [CallerMemberName] string? propertyName = null)
    {
        if (comparer.Equals(oldValue, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        callback(newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    //
    // Summary:
    //     Compares the current and new values for a given nested property. If the value
    //     has changed, raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the property and then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. The behavior mirrors that of CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String),
    //     with the difference being that this method is used to relay properties from a
    //     wrapped model in the current instance. This type is useful when creating wrapping,
    //     bindable objects that operate over models that lack support for notification
    //     (eg. for CRUD operations). Suppose we have this model (eg. for a database row
    //     in a table):
    //     public class Person
    //     {
    //         public string Name { get; set; }
    //     }
    //     We can then use a property to wrap instances of this type into our observable
    //     model (which supports notifications), injecting the notification to the properties
    //     of that model, like so:
    //     public class BindablePerson : ObservableObject
    //     {
    //         public Model { get; }
    //         public BindablePerson(Person model)
    //         {
    //             Model = model;
    //         }
    //         public string Name
    //         {
    //             get => Model.Name;
    //             set => Set(Model.Name, value, Model, (model, name) => model.Name = name);
    //         }
    //     }
    //     This way we can then use the wrapping object in our application, and all those
    //     "proxy" properties will also raise notifications when changed. Note that this
    //     method is not meant to be a replacement for CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String),
    //     and it should only be used when relaying properties to a model that doesn't support
    //     notifications, and only if you can't implement notifications to that model directly
    //     (eg. by having it inherit from CommunityToolkit.Mvvm.ComponentModel.ObservableObject).
    //     The syntax relies on passing the target model and a stateless callback to allow
    //     the C# compiler to cache the function, which results in much better performance
    //     and no memory usage.
    //
    // Parameters:
    //   oldValue:
    //     The current property value.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   model:
    //     The model containing the property being updated.
    //
    //   callback:
    //     The callback to invoke to set the target property value, if a change has occurred.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   TModel:
    //     The type of model whose property (or field) to set.
    //
    //   T:
    //     The type of property (or field) to set.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    protected bool SetProperty<TModel, T>(T oldValue, T newValue, TModel model, Action<TModel, T> callback, [CallerMemberName] string? propertyName = null) where TModel : class
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        callback(model, newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    //
    // Summary:
    //     Compares the current and new values for a given nested property. If the value
    //     has changed, raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the property and then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. The behavior mirrors that of CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String),
    //     with the difference being that this method is used to relay properties from a
    //     wrapped model in the current instance. See additional notes about this overload
    //     in CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``2(``1,``1,``0,System.Action{``0,``1},System.String).
    //
    // Parameters:
    //   oldValue:
    //     The current property value.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   comparer:
    //     The System.Collections.Generic.IEqualityComparer`1 instance to use to compare
    //     the input values.
    //
    //   model:
    //     The model containing the property being updated.
    //
    //   callback:
    //     The callback to invoke to set the target property value, if a change has occurred.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   TModel:
    //     The type of model whose property (or field) to set.
    //
    //   T:
    //     The type of property (or field) to set.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    protected bool SetProperty<TModel, T>(T oldValue, T newValue, IEqualityComparer<T> comparer, TModel model, Action<TModel, T> callback, [CallerMemberName] string? propertyName = null) where TModel : class
    {
        if (comparer.Equals(oldValue, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        callback(model, newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    //
    // Summary:
    //     Compares the current and new values for a given field (which should be the backing
    //     field for a property). If the value has changed, raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the field and then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. The behavior mirrors that of CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String),
    //     with the difference being that this method will also monitor the new value of
    //     the property (a generic System.Threading.Tasks.Task) and will also raise the
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged again for
    //     the target property when it completes. This can be used to update bindings observing
    //     that System.Threading.Tasks.Task or any of its properties. This method and its
    //     overload specifically rely on the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier
    //     type, which needs to be used in the backing field for the target System.Threading.Tasks.Task
    //     property. The field doesn't need to be initialized, as this method will take
    //     care of doing that automatically. The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier
    //     type also includes an implicit operator, so it can be assigned to any System.Threading.Tasks.Task
    //     instance directly. Here is a sample property declaration using this method:
    //     private TaskNotifier myTask;
    //     public Task MyTask
    //     {
    //         get => myTask;
    //         private set => SetAndNotifyOnCompletion(ref myTask, value);
    //     }
    //
    // Parameters:
    //   taskNotifier:
    //     The field notifier to modify.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    //     The return value being true only indicates that the new value being assigned
    //     to taskNotifier is different than the previous one, and it does not mean the
    //     new System.Threading.Tasks.Task instance passed as argument is in any particular
    //     state.
    protected bool SetPropertyAndNotifyOnCompletion([NotNull] ref TaskNotifier? taskNotifier, Task? newValue, [CallerMemberName] string? propertyName = null)
    {
        return SetPropertyAndNotifyOnCompletion(taskNotifier ?? (taskNotifier = new TaskNotifier()), newValue, delegate
        {
        }, propertyName);
    }

    //
    // Summary:
    //     Compares the current and new values for a given field (which should be the backing
    //     field for a property). If the value has changed, raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the field and then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. This method is just like CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetPropertyAndNotifyOnCompletion(CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier@,System.Threading.Tasks.Task,System.String),
    //     with the difference being an extra System.Action`1 parameter with a callback
    //     being invoked either immediately, if the new task has already completed or is
    //     null, or upon completion.
    //
    // Parameters:
    //   taskNotifier:
    //     The field notifier to modify.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   callback:
    //     A callback to invoke to update the property value.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    protected bool SetPropertyAndNotifyOnCompletion([NotNull] ref TaskNotifier? taskNotifier, Task? newValue, Action<Task?> callback, [CallerMemberName] string? propertyName = null)
    {
        return SetPropertyAndNotifyOnCompletion(taskNotifier ?? (taskNotifier = new TaskNotifier()), newValue, callback, propertyName);
    }

    //
    // Summary:
    //     Compares the current and new values for a given field (which should be the backing
    //     field for a property). If the value has changed, raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the field and then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. The behavior mirrors that of CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetProperty``1(``0@,``0,System.String),
    //     with the difference being that this method will also monitor the new value of
    //     the property (a generic System.Threading.Tasks.Task) and will also raise the
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged again for
    //     the target property when it completes. This can be used to update bindings observing
    //     that System.Threading.Tasks.Task or any of its properties. This method and its
    //     overload specifically rely on the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier`1
    //     type, which needs to be used in the backing field for the target System.Threading.Tasks.Task
    //     property. The field doesn't need to be initialized, as this method will take
    //     care of doing that automatically. The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier`1
    //     type also includes an implicit operator, so it can be assigned to any System.Threading.Tasks.Task
    //     instance directly. Here is a sample property declaration using this method:
    //     private TaskNotifier<int> myTask;
    //     public Task<int> MyTask
    //     {
    //         get => myTask;
    //         private set => SetAndNotifyOnCompletion(ref myTask, value);
    //     }
    //
    // Parameters:
    //   taskNotifier:
    //     The field notifier to modify.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   T:
    //     The type of result for the System.Threading.Tasks.Task`1 to set and monitor.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    //     The return value being true only indicates that the new value being assigned
    //     to taskNotifier is different than the previous one, and it does not mean the
    //     new System.Threading.Tasks.Task`1 instance passed as argument is in any particular
    //     state.
    protected bool SetPropertyAndNotifyOnCompletion<T>([NotNull] ref TaskNotifier<T>? taskNotifier, Task<T>? newValue, [CallerMemberName] string? propertyName = null)
    {
        return SetPropertyAndNotifyOnCompletion(taskNotifier ?? (taskNotifier = new TaskNotifier<T>()), newValue, delegate
        {
        }, propertyName);
    }

    //
    // Summary:
    //     Compares the current and new values for a given field (which should be the backing
    //     field for a property). If the value has changed, raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging
    //     event, updates the field and then raises the CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged
    //     event. This method is just like CommunityToolkit.Mvvm.ComponentModel.ObservableObject.SetPropertyAndNotifyOnCompletion``1(CommunityToolkit.Mvvm.ComponentModel.ObservableObject.TaskNotifier{``0}@,System.Threading.Tasks.Task{``0},System.String),
    //     with the difference being an extra System.Action`1 parameter with a callback
    //     being invoked either immediately, if the new task has already completed or is
    //     null, or upon completion.
    //
    // Parameters:
    //   taskNotifier:
    //     The field notifier to modify.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   callback:
    //     A callback to invoke to update the property value.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   T:
    //     The type of result for the System.Threading.Tasks.Task`1 to set and monitor.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    //
    // Remarks:
    //     The CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanging and
    //     CommunityToolkit.Mvvm.ComponentModel.ObservableObject.PropertyChanged events
    //     are not raised if the current and new value for the target property are the same.
    protected bool SetPropertyAndNotifyOnCompletion<T>([NotNull] ref TaskNotifier<T>? taskNotifier, Task<T>? newValue, Action<Task<T>?> callback, [CallerMemberName] string? propertyName = null)
    {
        return SetPropertyAndNotifyOnCompletion(taskNotifier ?? (taskNotifier = new TaskNotifier<T>()), newValue, callback, propertyName);
    }

    //
    // Summary:
    //     Implements the notification logic for the related methods.
    //
    // Parameters:
    //   taskNotifier:
    //     The field notifier.
    //
    //   newValue:
    //     The property's value after the change occurred.
    //
    //   callback:
    //     A callback to invoke to update the property value.
    //
    //   propertyName:
    //     (optional) The name of the property that changed.
    //
    // Type parameters:
    //   TTask:
    //     The type of System.Threading.Tasks.Task to set and monitor.
    //
    // Returns:
    //     true if the property was changed, false otherwise.
    private bool SetPropertyAndNotifyOnCompletion<TTask>(ITaskNotifier<TTask> taskNotifier, TTask? newValue, Action<TTask?> callback, [CallerMemberName] string? propertyName = null) where TTask : Task
    {
        TTask newValue2 = newValue;
        ITaskNotifier<TTask> taskNotifier2 = taskNotifier;
        string propertyName2 = propertyName;
        Action<TTask?> callback2 = callback;
        if (taskNotifier2.Task == newValue2)
        {
            return false;
        }

        bool num = newValue2?.IsCompleted ?? true;
        OnPropertyChanging(propertyName2);
        taskNotifier2.Task = newValue2;
        OnPropertyChanged(propertyName2);
        if (num)
        {
            callback2(newValue2);
            return true;
        }

        MonitorTask();
        return true;
        async void MonitorTask()
        {
            try
            {
                await newValue2;
            }
            catch
            {
            }

            if (taskNotifier2.Task == newValue2)
            {
                OnPropertyChanged(propertyName2);
            }

            callback2(newValue2);
        }
    }
}
