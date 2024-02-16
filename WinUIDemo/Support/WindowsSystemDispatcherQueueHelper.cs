#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinUIDemo;

/// <summary>
/// Helper for applying a <see cref="Microsoft.UI.Xaml.Media.AcrylicBrush"/>.
/// </summary>
public class WindowsSystemDispatcherQueueHelper
{
    object? _dispatcherQueueController = null;

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        try
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {   // One already exists, so we'll just use it.
                return;
            }

            if (_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA
                CreateDispatcherQueueController(options, ref _dispatcherQueueController);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EnsureWindowsSystemDispatcherQueueController: {ex.Message}");
        }
    }

    [DllImport("CoreMessaging.dll")]
    static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController);

    [StructLayout(LayoutKind.Sequential)]
    struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }
}
