using System;
using System.Collections.Generic;

using Windows.System;
using Windows.UI.Core;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace WinUIDemo;

/*
 * 
 *   This class is meant to extend a Window/Page/UserControl mouse events, e.g. PointerWheelChanged.
 *   
 */

#region [Interfaces]
public interface IMouseCommand<in T>
{
    bool Hit(
        bool ctrlDown,
        bool altDown,
        bool shiftDown,
        bool leftButtonDown,
        bool middleButtonDown,
        bool rightButtonDown);

    bool ShouldHandleAfterExecution();

    bool ShouldSwallowAfterExecution();

    void Execute(T args);
}
#endregion

#region [Implementations]
public class MouseCommand<T> : IMouseCommand<T>
{
    private readonly bool _ctrl;
    private readonly bool _alt;
    private readonly bool _shift;
    private readonly bool _leftButton;
    private readonly bool _middleButton;
    private readonly bool _rightButton;
    private readonly Action<T> _action;
    private readonly bool _shouldHandle;
    private readonly bool _shouldSwallow;

    public MouseCommand(
        bool leftButtonDown,
        bool middleButtonDown,
        bool rightButtonDown,
        Action<T> action,
        bool shouldHandle = true,
        bool shouldSwallow = true) :
        this(false, false, false, leftButtonDown, middleButtonDown, rightButtonDown, action, shouldHandle, shouldSwallow)
    {
    }

    public MouseCommand(
        bool ctrlDown,
        bool altDown,
        bool shiftDown,
        bool leftButtonDown,
        bool middleButtonDown,
        bool rightButtonDown,
        Action<T> action,
        bool shouldHandle = true,
        bool shouldSwallow = true)
    {
        _ctrl = ctrlDown;
        _alt = altDown;
        _shift = shiftDown;
        _leftButton = leftButtonDown;
        _middleButton = middleButtonDown;
        _rightButton = rightButtonDown;
        _action = action;
        _shouldHandle = shouldHandle;
        _shouldSwallow = shouldSwallow;
    }

    public bool Hit(
        bool ctrlDown,
        bool altDown,
        bool shiftDown,
        bool leftButtonDown,
        bool middleButtonDown,
        bool rightButtonDown)
    {
        return _ctrl == ctrlDown &&
               _alt == altDown &&
               _shift == shiftDown &&
               _leftButton == leftButtonDown &&
               _middleButton == middleButtonDown &&
               _rightButton == rightButtonDown;
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

public class MouseCommandHandler : ICommandHandler<PointerRoutedEventArgs>
{
    readonly bool UWP = false;

    public readonly ICollection<IMouseCommand<PointerRoutedEventArgs>> Commands;

    private readonly UIElement _relativeTo;

    public MouseCommandHandler(ICollection<IMouseCommand<PointerRoutedEventArgs>> commands, UIElement relativeTo)
    {
        Commands = commands;
        _relativeTo = relativeTo;
    }

    public CommandHandlerResult Handle(PointerRoutedEventArgs args)
    {
        bool ctrlDown = false;
        bool altDown = false;
        bool shiftDown = false;

        var point = args.GetCurrentPoint(_relativeTo).Properties;
        var shouldHandle = false;
        var shouldSwallow = false;

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


        foreach (var command in Commands)
        {
            if (command.Hit(
                ctrlDown,
                altDown,
                shiftDown,
                point.IsLeftButtonPressed,
                point.IsMiddleButtonPressed,
                point.IsRightButtonPressed))
            {
                command.Execute(args);

                if (command.ShouldSwallowAfterExecution())
                {
                    shouldSwallow = true;
                }

                if (command.ShouldHandleAfterExecution())
                {
                    shouldHandle = true;
                }

                break;
            }
        }

        return new CommandHandlerResult(shouldHandle, shouldSwallow);
    }
}
#endregion