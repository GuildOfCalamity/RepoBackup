using System;
using System.Collections.Generic;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

using Windows.System;
using Windows.UI.Core;

namespace WinUIDemo;

/*
 * 
 *   This class is meant to extend the Window/Page/UserControl keyboard events, e.g. KeyDown/KeyUp.
 *   
 */

#region [Interfaces]
public interface IKeyboardCommand<T>
{
    bool Hit(bool ctrlDown, bool altDown, bool shiftDown, VirtualKey key);
    bool ShouldExecute(IKeyboardCommand<T> lastCommand);
    bool ShouldHandleAfterExecution();
    bool ShouldSwallowAfterExecution();
    void Execute(T args);
}
#endregion

#region [Implementations]
public class KeyboardCommand<T> : IKeyboardCommand<T>
{
    private static readonly TimeSpan ConsecutiveHitsInterval = TimeSpan.FromMilliseconds(500);

    private readonly bool _ctrl;
    private readonly bool _alt;
    private readonly bool _shift;
    private readonly IList<VirtualKey> _keys;
    private readonly Action<T> _action;
    private readonly bool _shouldHandle;
    private readonly bool _shouldSwallow;
    private readonly int _requiredHits;
    private int _hits;
    private DateTime _lastHitTimestamp;

    public KeyboardCommand(
        VirtualKey key,
        Action<T> action,
        bool shouldHandle = true,
        bool shouldSwallow = true) :
        this(false, false, false, key, action, shouldHandle, shouldSwallow)
    {
    }

    public KeyboardCommand(
        bool ctrlDown,
        bool altDown,
        bool shiftDown,
        VirtualKey key,
        Action<T> action,
        bool shouldHandle = true,
        bool shouldSwallow = true,
        int requiredHits = 1) :
        this(ctrlDown, altDown, shiftDown, new List<VirtualKey>() { key }, action, shouldHandle, shouldSwallow, requiredHits)
    {
    }

    public KeyboardCommand(
        bool ctrlDown,
        bool altDown,
        bool shiftDown,
        IList<VirtualKey> keys,
        Action<T> action,
        bool shouldHandle,
        bool shouldSwallow,
        int requiredHits = 1)
    {
        _ctrl = ctrlDown;
        _alt = altDown;
        _shift = shiftDown;
        _keys = keys ?? new List<VirtualKey>();
        _action = action;
        _shouldHandle = shouldHandle;
        _shouldSwallow = shouldSwallow;
        _requiredHits = requiredHits;
        _hits = 0;
        _lastHitTimestamp = DateTime.MinValue;
    }

    public bool Hit(bool ctrlDown, bool altDown, bool shiftDown, VirtualKey key)
    {
        return _ctrl == ctrlDown && _alt == altDown && _shift == shiftDown && _keys.Contains(key);
    }

    public bool ShouldExecute(IKeyboardCommand<T> lastCommand)
    {
        DateTime now = DateTime.UtcNow;

        if (lastCommand == this && now - _lastHitTimestamp < ConsecutiveHitsInterval)
            _hits++;
        else
            _hits = 1;

        _lastHitTimestamp = now;

        if (_hits >= _requiredHits)
        {
            _hits = 0;
            return true;
        }

        return false;
    }

    public bool ShouldHandleAfterExecution()
    {
        return _shouldHandle;
    }

    public bool ShouldSwallowAfterExecution()
    {
        return _shouldSwallow;
    }

    public void Execute(T args)
    {
        _action?.Invoke(args);
    }

 
}

public class KeyboardCommandHandler : ICommandHandler<KeyRoutedEventArgs>
{
    readonly bool UWP = false;

    public readonly ICollection<IKeyboardCommand<KeyRoutedEventArgs>> Commands;

    private IKeyboardCommand<KeyRoutedEventArgs> _lastCommand;

    public KeyboardCommandHandler(ICollection<IKeyboardCommand<KeyRoutedEventArgs>> commands)
    {
        Commands = commands;
    }

    public CommandHandlerResult Handle(KeyRoutedEventArgs args)
    {
        bool ctrlDown = false;
        bool altDown = false;
        bool shiftDown = false;

        // We'll need to switch to Microsoft.UI.Input for a WinUI3 application.
        if (UWP)
        {
            ctrlDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            altDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            shiftDown = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        }
        else
        {
            // https://stackoverflow.com/questions/76535706/easiest-way-to-set-the-window-background-to-an-acrylic-brush-in-winui3/76536129#76536129
            ctrlDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            altDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            shiftDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        }

        var shouldHandle = false;
        var shouldSwallow = false;

        foreach (var command in Commands)
        {
            if (command.Hit(ctrlDown, altDown, shiftDown, args.Key))
            {
                if (command.ShouldExecute(_lastCommand))
                    command.Execute(args);

                if (command.ShouldSwallowAfterExecution())
                    shouldSwallow = true;

                if (command.ShouldHandleAfterExecution())
                    shouldHandle = true;

                _lastCommand = command;
                break;
            }
        }

        if (!shouldHandle)
            _lastCommand = null;

        return new CommandHandlerResult(shouldHandle, shouldSwallow);
    }
}
#endregion
