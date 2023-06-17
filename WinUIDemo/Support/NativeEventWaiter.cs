using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace WinUIDemo.Support;

public static class NativeEventWaiter
{
    public static event Action<string> OnWaitTimeout = (name) => { };
    public static event Action<string> OnWaitExit = (name) => { };

    /// <summary>
    /// Invokes the action (using the Dispatcher) once the WaitHandle is triggered.
    /// </summary>
    public static void WaitForEventLoop(string eventName, Action callback, Microsoft.UI.Dispatching.DispatcherQueue dispatcher, CancellationToken cancel)
    {
        new Thread(() =>
        {
            var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
            while (!cancel.IsCancellationRequested)
            {
                if (WaitHandle.WaitAny(new WaitHandle[] { cancel.WaitHandle, eventHandle }) == 1)
                {
                    Debug.WriteLine($"[WaitHandle Triggered] '{eventName}' HasThreadAccess: {dispatcher.HasThreadAccess}");
                    dispatcher.EnqueueAsync(callback);
                }
                else
                {
                    Debug.WriteLine($"[Leaving WaitForEventLoop] '{eventName}'");
                    return;
                }
                Thread.Sleep(1);
            }
        }).Start();
    }

    /// <summary>
    /// Invokes the action once the WaitHandle is triggered.
    /// </summary>
    public static void WaitForEventLoop(string eventName, Action callback, CancellationToken cancel)
    {
        new Thread(() =>
        {
            var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
            while (!cancel.IsCancellationRequested)
            {
                if (WaitHandle.WaitAny(new WaitHandle[] { cancel.WaitHandle, eventHandle }) == 1)
                {
                    Debug.WriteLine($"[WaitHandle Triggered] '{eventName}'");
                    callback();
                }
                else
                {
                    Debug.WriteLine($"[Leaving WaitForEventLoop] '{eventName}'");
                    return;
                }
                Thread.Sleep(1);
            }
        }).Start();
    }

    /// <summary>
    /// Invokes the action once the WaitHandle is triggered (with timeout option).
    /// Waits x number of times based on the exitAmount.
    /// </summary>
    public static void WaitForEventLoop(string eventName, Action callback, CancellationToken cancel, int waitTimeout, int exitAmount)
    {
        if (exitAmount < 1) { exitAmount = 1; }

        new Thread(() =>
        {
            int waitCount = 0;
            var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
            while (!cancel.IsCancellationRequested && (waitCount < exitAmount))
            {
                if (WaitHandle.WaitAny(new WaitHandle[] { cancel.WaitHandle, eventHandle }, waitTimeout) == 1)
                {
                    Debug.WriteLine($"[WaitHandle Triggered] '{eventName}'");
                    callback();
                }
                else
                {
                    waitCount++;
                    OnWaitTimeout?.Invoke($"{eventName}");
                    Debug.WriteLine($"[WaitHandle Timeout #{waitCount}] '{eventName}'");
                }
                Thread.Sleep(1);
            }
            OnWaitExit?.Invoke($"{eventName}");
            Debug.WriteLine($"[Leaving WaitForEventLoop] '{eventName}'");
        }).Start();
    }

    /// <summary>
    /// Testing method.
    /// </summary>
    public static void SpawnAndWait(IEnumerable<Action> actions)
    {
        var list = actions.ToList<Action>();
        var handles = new ManualResetEvent[actions.Count()];
        for (var i = 0; i < list.Count; i++)
        {
            handles[i] = new ManualResetEvent(false);
            Action currentAction = list[i];
            ManualResetEvent currentHandle = handles[i];
            Action wrappedAction = () =>
            {
                try { currentAction(); }
                catch (Exception ex) { Debug.WriteLine($"SpawnAndWait: {ex.Message}", ex); }
                finally { currentHandle.Set(); }
            };
            ThreadPool.QueueUserWorkItem(x => wrappedAction());
        }
        WaitHandle.WaitAll(handles);
    }
}
