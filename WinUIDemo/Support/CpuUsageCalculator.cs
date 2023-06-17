using System;
using System.Diagnostics;
using System.Threading;

namespace WinUIDemo;

/// <summary>
/// This is meant to run in a separate thread, do not call this directly in your main UI thread.
/// </summary>
public static class CpuUsageCalculator
{
    static DateTime lastTime;
    static TimeSpan lastTotalProcessorTime;
    static DateTime currentTime;
    static TimeSpan currentTotalProcessorTime;
    /// <summary>
    /// Determine CPU usage based on the difference between last CPU time and the current CPU time.
    /// </summary>
    /// <remarks>
    /// We won't declare this as an async Task method since we'll be using ref params. For an async
    /// method the compiler generates a state machine that represents the execution flow of the method 
    /// therefore the state machine would need to capture and propagate the state of the ref/out params 
    /// across different points of suspension and resumption. We could use a mutable data structure to
    /// capture and update the required values within the async method, but this way is much simpler.
    /// </remarks>
    /// <param name="processName">the process name to look for</param>
    /// <param name="frequency">polling time</param>
    /// <param name="result">the CPU usage text to display elsewhere</param>
    /// <param name="running">flag to exit while loop</param>
    public static void Run(string processName, int frequency, ref string result, ref bool running)
    {
        while (running)
        {
            try
            {
                if (string.IsNullOrEmpty(processName))
                {
                    var tmp = System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly.FullName;
                    processName = tmp?.Split(',')[0];
                }

                Process[] pp = Process.GetProcessesByName(processName);

                if (pp.Length == 0)
                {
                    result = $"'{processName}' not found";
                }
                else
                {
                    Process p = pp[0];
                    
                    if (p == null)
                        continue;

                    if (lastTime == null || 
                        lastTime == DateTime.MinValue || 
                        lastTime == DateTime.MaxValue || 
                        lastTime == new DateTime())
                    {
                        lastTime = DateTime.Now;
                        lastTotalProcessorTime = p.TotalProcessorTime;
                    }
                    else
                    {
                        currentTime = DateTime.Now;
                        currentTotalProcessorTime = p.TotalProcessorTime;
                        double CPUUsage = (currentTotalProcessorTime.TotalMilliseconds - lastTotalProcessorTime.TotalMilliseconds) / currentTime.Subtract(lastTime).TotalMilliseconds / Convert.ToDouble(Environment.ProcessorCount);
                        lastTime = currentTime;
                        lastTotalProcessorTime = currentTotalProcessorTime;

                        //result = string.Format("{0} CPU: {1:0.0}%", processName, CPUUsage * 100);
                        result = string.Format("{0:0.0}%", CPUUsage * 100);
                    }
                }
            }
            catch (Exception) { }

            // Update result every n milliseconds.
            Thread.Sleep(frequency);
        }
    }
}

/// <summary>
/// These alternative methods did not seem to be accurate.
/// Maybe we'll revisit them in the future.
/// </summary>
public static class CpuCalculatorAlternative
{
    static Windows.System.Diagnostics.ProcessCpuUsage cpuUsage;

    /// <summary>
    /// Attempts to calculate the CPU % use based on the <see cref="Windows.System.Diagnostics.ProcessDiagnosticInfo"/>.
    /// </summary>
    /// <returns>percentage of CPU use</returns>
    public static float GetCpuUsage()
    {
        if (cpuUsage == null)
        {
            cpuUsage = Windows.System.Diagnostics.ProcessDiagnosticInfo.GetForCurrentProcess().CpuUsage;
        }

        // Get the CPU usage percentage
        TimeSpan cpuTime = cpuUsage.GetReport().KernelTime + cpuUsage.GetReport().UserTime;
        float cpuUsagePercentage = (float)(cpuTime.TotalMilliseconds / (Environment.ProcessorCount * 10));
        Debug.WriteLine($"CPU Usage: {cpuUsagePercentage}%");

        return cpuUsagePercentage;
    }

    /// <summary>
    /// Attempts to calculate the CPU % use using the WinAPI call <see cref="NativeMethods.GetSystemTimes"/>.
    /// </summary>
    /// <returns>percentage of CPU use</returns>
    public static float GetCpuUsage2()
    {
        long idleTime = 0;
        long kernelTime = 0;
        long userTime = 0;

        if (!NativeMethods.GetSystemTimes(out idleTime, out kernelTime, out userTime))
        {
            Debug.WriteLine($"** NativeMethods.GetSystemTimes() failed! **");
            return 0f;
        }

        // Calculate the CPU usage percentage
        long totalTime = kernelTime + userTime;
        long elapsedTime = totalTime - idleTime;

        float cpuUsage = (float)(100.0 * elapsedTime / totalTime);
        Debug.WriteLine($"CPU Usage: {cpuUsage}%");

        return cpuUsage;
    }
}
