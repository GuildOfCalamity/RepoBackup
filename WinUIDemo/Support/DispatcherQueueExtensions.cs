using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WinUIDemo;

/// <summary>
/// Helpers for executing code using the <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/>.
/// I have converted this from the UWP "System.Windows.DispatcherQueue" to the WinUI "Microsoft.UI.Dispatching.DispatcherQueue".
/// </summary>
public static class DispatcherQueueExtensions
{
    /// <summary>
    /// Indicates whether or not <see cref="Microsoft.UI.Dispatching.DispatcherQueue.HasThreadAccess"/> is available.
    /// This check is not necessary when using a newer WindowsAppSDK package.
    /// </summary>
    //private static readonly bool IsHasThreadAccessPropertyAvailable = ApiInformation.IsMethodPresent("Windows.System.DispatcherQueue", "HasThreadAccess");
    private static readonly bool IsHasThreadAccessPropertyAvailable = Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Microsoft.UI.Dispatching.DispatcherQueue", "HasThreadAccess");

    /// <summary>
    /// Enqueues an action using the <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/>.
    /// Wraps the call in a try/catch and returns the result as a <see cref="Task"/>.
    /// This would typically be called from an asynchronous event, such as <see cref="Windows.System.Power.PowerManager.EnergySaverStatusChanged"/>
    /// </summary>
    /// <example>
    /// Dispatcher.CallOnUIThread(() => { TextBlock.SelectionHighlightColor = new SolidColorBrush(Colors.Yellow); });
    /// </example>
    public static Task CallOnUIThread(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Microsoft.UI.Dispatching.DispatcherQueueHandler handler)
    {
        try
        { 
            _ = dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, handler);
            return Task.CompletedTask;
        }
        catch (Exception e)
        {
            return Task.FromException(e);
        }
    }

    /// <summary>
    /// Invokes a given function on the target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> and returns a
    /// <see cref="Task"/> that completes when the invocation of the function is completed.
    /// </summary>
    /// <param name="dispatcher">The target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> to invoke the code on.</param>
    /// <param name="function">The <see cref="Action"/> to invoke.</param>
    /// <param name="priority">The priority level for the function to invoke.</param>
    /// <returns>A <see cref="Task"/> that completes when the invocation of <paramref name="function"/> is over.</returns>
    /// <remarks>If the current thread has access to <paramref name="dispatcher"/>, <paramref name="function"/> will be invoked directly.</remarks>
    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Action function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority = Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal)
    {
        // Run the function directly when we have thread access.
        // Also reuse Task.CompletedTask in case of success,
        // to skip an unnecessary heap allocation for every invocation.
        if (dispatcher.HasThreadAccess) //if (IsHasThreadAccessPropertyAvailable && dispatcher.HasThreadAccess)
        {
            try
            {
                function();

                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        static Task TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Action function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority)
        {
            var taskCompletionSource = new TaskCompletionSource<object?>();

            if (!dispatcher.TryEnqueue(priority, () =>
            {
                try
                {
                    function();

                    taskCompletionSource.SetResult(null);
                }
                catch (Exception e)
                {
                    taskCompletionSource.SetException(e);
                }
            }))
            {
                taskCompletionSource.SetException(GetEnqueueException("Failed to enqueue the operation"));
            }

            return taskCompletionSource.Task;
        }

        return TryEnqueueAsync(dispatcher, function, priority);
    }

    /// <summary>
    /// Invokes a given function on the target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> and returns a
    /// <see cref="Task{TResult}"/> that completes when the invocation of the function is completed.
    /// </summary>
    /// <typeparam name="T">The return type of <paramref name="function"/> to relay through the returned <see cref="Task{TResult}"/>.</typeparam>
    /// <param name="dispatcher">The target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> to invoke the code on.</param>
    /// <param name="function">The <see cref="Func{TResult}"/> to invoke.</param>
    /// <param name="priority">The priority level for the function to invoke.</param>
    /// <returns>A <see cref="Task"/> that completes when the invocation of <paramref name="function"/> is over.</returns>
    /// <remarks>If the current thread has access to <paramref name="dispatcher"/>, <paramref name="function"/> will be invoked directly.</remarks>
    public static Task<T> EnqueueAsync<T>(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<T> function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority = Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal)
    {
        // If we have thread access, we can retrieve the task directly.
        // We don't use ConfigureAwait(false) in this case, in order
        // to let the caller continue its execution on the same thread
        // after awaiting the task returned by this function.
        if (dispatcher.HasThreadAccess) //if (IsHasThreadAccessPropertyAvailable && dispatcher.HasThreadAccess)
        {
            try
            {
                return Task.FromResult(function());
            }
            catch (Exception e)
            {
                return Task.FromException<T>(e);
            }
        }

        static Task<T> TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<T> function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();

            if (!dispatcher.TryEnqueue(priority, () =>
            {
                try
                {
                    taskCompletionSource.SetResult(function());
                }
                catch (Exception e)
                {
                    taskCompletionSource.SetException(e);
                }
            }))
            {
                taskCompletionSource.SetException(GetEnqueueException("Failed to enqueue the operation"));
            }

            return taskCompletionSource.Task;
        }

        return TryEnqueueAsync(dispatcher, function, priority);
    }

    /// <summary>
    /// Invokes a given function on the target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> and returns a
    /// <see cref="Task"/> that acts as a proxy for the one returned by the given function.
    /// </summary>
    /// <param name="dispatcher">The target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> to invoke the code on.</param>
    /// <param name="function">The <see cref="Func{TResult}"/> to invoke.</param>
    /// <param name="priority">The priority level for the function to invoke.</param>
    /// <returns>A <see cref="Task"/> that acts as a proxy for the one returned by <paramref name="function"/>.</returns>
    /// <remarks>If the current thread has access to <paramref name="dispatcher"/>, <paramref name="function"/> will be invoked directly.</remarks>
    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<Task> function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority = Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal)
    {
        // If we have thread access, we can retrieve the task directly.
        // We don't use ConfigureAwait(false) in this case, in order
        // to let the caller continue its execution on the same thread
        // after awaiting the task returned by this function.
        if (dispatcher.HasThreadAccess) //if (IsHasThreadAccessPropertyAvailable && dispatcher.HasThreadAccess)
        {
            try
            {
                if (function() is Task awaitableResult)
                {
                    return awaitableResult;
                }

                return Task.FromException(GetEnqueueException($"The Task returned by {nameof(function)} cannot be null."));
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        static Task TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<Task> function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority)
        {
            var taskCompletionSource = new TaskCompletionSource<object?>();

            if (!dispatcher.TryEnqueue(priority, async () =>
            {
                try
                {
                    if (function() is Task awaitableResult)
                    {
                        await awaitableResult.ConfigureAwait(false);

                        taskCompletionSource.SetResult(null);
                    }
                    else
                    {
                        taskCompletionSource.SetException(GetEnqueueException($"The Task returned by {nameof(function)} cannot be null."));
                    }
                }
                catch (Exception e)
                {
                    taskCompletionSource.SetException(e);
                }
            }))
            {
                taskCompletionSource.SetException(GetEnqueueException("Failed to enqueue the operation"));
            }

            return taskCompletionSource.Task;
        }

        return TryEnqueueAsync(dispatcher, function, priority);
    }

    /// <summary>
    /// Invokes a given function on the target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> and returns a
    /// <see cref="Task{TResult}"/> that acts as a proxy for the one returned by the given function.
    /// </summary>
    /// <typeparam name="T">The return type of <paramref name="function"/> to relay through the returned <see cref="Task{TResult}"/>.</typeparam>
    /// <param name="dispatcher">The target <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> to invoke the code on.</param>
    /// <param name="function">The <see cref="Func{TResult}"/> to invoke.</param>
    /// <param name="priority">The priority level for the function to invoke.</param>
    /// <returns>A <see cref="Task{TResult}"/> that relays the one returned by <paramref name="function"/>.</returns>
    /// <remarks>If the current thread has access to <paramref name="dispatcher"/>, <paramref name="function"/> will be invoked directly.</remarks>
    public static Task<T> EnqueueAsync<T>(this Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<Task<T>> function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority = Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal)
    {
        if (dispatcher.HasThreadAccess) //if (IsHasThreadAccessPropertyAvailable && dispatcher.HasThreadAccess)
        {
            try
            {
                if (function() is Task<T> awaitableResult)
                {
                    return awaitableResult;
                }

                return Task.FromException<T>(GetEnqueueException($"The Task returned by {nameof(function)} cannot be null."));
            }
            catch (Exception e)
            {
                return Task.FromException<T>(e);
            }
        }

        static Task<T> TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Func<Task<T>> function, Microsoft.UI.Dispatching.DispatcherQueuePriority priority)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();

            if (!dispatcher.TryEnqueue(priority, async () =>
            {
                try
                {
                    if (function() is Task<T> awaitableResult)
                    {
                        var result = await awaitableResult.ConfigureAwait(false);

                        taskCompletionSource.SetResult(result);
                    }
                    else
                    {
                        taskCompletionSource.SetException(GetEnqueueException($"The Task returned by {nameof(function)} cannot be null."));
                    }
                }
                catch (Exception e)
                {
                    taskCompletionSource.SetException(e);
                }
            }))
            {
                taskCompletionSource.SetException(GetEnqueueException("Failed to enqueue the operation"));
            }

            return taskCompletionSource.Task;
        }

        return TryEnqueueAsync(dispatcher, function, priority);
    }

    /// <summary>
    /// Creates an <see cref="InvalidOperationException"/> to return when an enqueue operation fails.
    /// </summary>
    /// <param name="message">The message of the exception.</param>
    /// <returns>An <see cref="InvalidOperationException"/> with a specified message.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)] // Prevent the JIT compiler from inlining this method with the caller.
    private static InvalidOperationException GetEnqueueException(string message)
    {
        return new InvalidOperationException(message);
    }


	/// <summary>
	/// Used to debounce (rate-limit) an event.  The action will be postponed and executed after the interval has elapsed.
	/// At the end of the interval, the function will be called with the arguments that were passed most recently to the debounced function.
	/// Use this method to control the timer instead of calling Start/Interval/Stop manually.
	/// A scheduled debounce can still be stopped by calling the stop method on the timer instance.
	/// Each timer can only have one debounced function limited at a time.
	/// </summary>
	/// <param name="timer">Timer instance, only one debounced function can be used per timer.</param>
	/// <param name="action">Action to execute at the end of the interval.</param>
	/// <param name="interval">Interval to wait before executing the action.</param>
	/// <param name="immediate">Determines if the action execute on the leading edge instead of trailing edge.</param>
	/// <example>
	/// <code>
	/// private DispatcherQueueTimer _timer = DispatcherQueue.CreateTimer();
	/// _timer.Debounce(async () => {
	///    Debug.WriteLine($"Debounce => {DateTime.Now.ToString("hh:mm:ss.fff tt")}"); // Only executes this code after half a second has elapsed since last trigger.
	/// }, TimeSpan.FromSeconds(0.5));
	/// </code>
	/// </example>
	public static void Debounce(this Microsoft.UI.Dispatching.DispatcherQueueTimer timer, Action action, TimeSpan interval, bool immediate = false)
    {
        // Check and stop any existing timer
        var timeout = timer.IsRunning;
        if (timeout)
            timer.Stop();

        // Reset timer parameters
        timer.Tick -= DebounceTimerTick;
        timer.Interval = interval;

        if (immediate)
        {   // If we're in immediate mode then we only execute if the timer wasn't running beforehand
            if (!timeout)
                action.Invoke();
        }
        else
        {   // If we're not in immediate mode, then we'll execute when the current timer expires.
            timer.Tick += DebounceTimerTick;

            // Store/Update function
            _debounceInstances.AddOrUpdate(timer, action, (k, v) => v);
        }

        // Start the timer to keep track of the last call here.
        timer.Start();
    }
    /// <summary>
    /// This event is only registered/run if we weren't in immediate mode above.
    /// </summary>
    static void DebounceTimerTick(object sender, object e)
    {
        if (sender is Microsoft.UI.Dispatching.DispatcherQueueTimer timer)
        {
            timer.Tick -= DebounceTimerTick;
            timer.Stop();

			// Extract the code an execute it.
			if (_debounceInstances.TryRemove(timer, out Action action))
                action?.Invoke();
        }
    }
    /// <summary>
    /// Our collection which holds timers for the dispatcher.
    /// </summary>
    static ConcurrentDictionary<Microsoft.UI.Dispatching.DispatcherQueueTimer, Action> _debounceInstances = new ConcurrentDictionary<Microsoft.UI.Dispatching.DispatcherQueueTimer, Action>();
}
