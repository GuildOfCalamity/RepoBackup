using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Windows.ApplicationModel.Core;
using Windows.Storage;

namespace WinUIDemo.Support;

public class PackagedLoggingService
{
    private const string MessageFormatString = "{0} [{1}] {2}"; // {timestamp} [{level}] {message}

    private static readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
    private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
    private static readonly TimeSpan LoggingInterval = TimeSpan.FromSeconds(10);
    private static readonly List<string> Messages = new List<string>();

    private static StorageFile _logFile;
    private static Task _backgroundTask;
    private static bool _initialized;

    #region [Public Methods]
    public static async Task InitializeFileSystemLoggingAsync()
    {
        if (_initialized)
            return;

        //CoreApplication.Suspending += async (sender, args) => { await TryFlushMessageQueueAsync(); };
        //CoreApplication.Resuming += async (sender, args) => { await InitializeLogFileWriterBackgroundTaskAsync(); };
        App.OnWindowClosing += async (args) => { await TryFlushMessageQueueAsync(); };

        await InitializeLogFileWriterBackgroundTaskAsync();
    }

    public static StorageFile GetLogFile()
    {
        return _logFile;
    }

    public static void Log(string message, LogLevel level = LogLevel.Debug, bool consoleOnly = false)
    {
        LogMessage($"{level}", message, consoleOnly);
    }

    public static void LogException(Exception ex, bool consoleOnly = false)
    {
        if (ex == null)
            return;

        Log(ex.ToString(), LogLevel.Error, consoleOnly);
    }
    #endregion

    #region [Private Methods]
    static void LogMessage(string level, string message, bool consoleOnly)
    {
        string timeStamp = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
        string formattedMessage = string.Format(MessageFormatString, timeStamp, level, message);

        // Print to console
        Debug.WriteLine(formattedMessage);

        if (!_initialized)
            return;

        if (!consoleOnly) // Add to message queue
            MessageQueue.Enqueue(formattedMessage);
    }

	/// <summary>
	/// Setup and config method.
	/// </summary>
	/// <returns>true if successful, false otherwise</returns>
	static async Task<bool> InitializeLogFileWriterBackgroundTaskAsync()
    {
        await SemaphoreSlim.WaitAsync();

        if (_backgroundTask != null && !_backgroundTask.IsCompleted)
        {
            SemaphoreSlim.Release();
            return false;
        }

        try
        {
            // Create our log file and folder if it does not exist already.
            if (_logFile == null)
            {
                StorageFolder logsFolder = await PackagedFileSystemUtility.GetOrCreateAppFolder("Logs");
                _logFile = await PackagedFileSystemUtility.CreateFile(logsFolder, DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + ".log");
            }

			// Begin thread loop to check MessageQueue and subsequently write to disk if messages are present.
			_backgroundTask = Task.Run(WriteLogMessages);

            _initialized = true;
            Log($"Log file location: {_logFile.Path}", LogLevel.Info, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            SemaphoreSlim.Release();
        }
        return false;
    }

    /// <summary>
    /// Thread loop method.
    /// </summary>
    static async Task WriteLogMessages()
    {
        while (true)
        {
            // Relax this loop so processor load is reduced.
            Thread.Sleep(LoggingInterval);

            // We will try to write all pending messages in our next attempt, if the current attempt failed
            // However, if the size of messages has become abnormally big, we know something is wrong and should abort at this point
            if (!await TryFlushMessageQueueAsync() && Messages.Count > 1000)
            {
                break;
            }
        }
    }

    /// <summary>
    /// The main work method.
    /// </summary>
    /// <returns>true if successful, false otherwise</returns>
    static async Task<bool> TryFlushMessageQueueAsync()
    {
        if (!_initialized)
            return false;

        await SemaphoreSlim.WaitAsync();

        try
        {
            if (MessageQueue.Count == 0)
                return true;

            // While there are items in the queue we will move them to the List<string>
            // and then utilize the Windows.Storage.FileIO to write the contents to disk.
            while (MessageQueue.TryDequeue(out string message))
            {
                Messages.Add(message);
            }

            await FileIO.AppendLinesAsync(_logFile, Messages);
            Messages.Clear();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            SemaphoreSlim.Release();
        }

        return false;
    }
    #endregion
}
