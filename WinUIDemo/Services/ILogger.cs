using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace WinUIDemo
{
    public enum LogLevel
    {
        Off = 0,
        Debug = 1,
        Info = 2,
        Success = 3,
        Important = 4,
        Notice = 5,
        Warning = 6,
        Error = 7,
        Critical = 8
    }

    public interface ILogger
    {
        LogLevel logLevel { get; set; }

        #region [Event definitions]
        /// <summary>
        /// An event that fires when a message is logged.
        /// </summary>
        event Action<string, LogLevel> OnLogMessage;

        /// <summary>
        /// An event that fires when the old log cleaning completes.
        /// </summary>
        event Action<string> OnPurgeComplete;

        /// <summary>
        /// An event that fires when an exception is thrown.
        /// </summary>
        event Action<Exception> OnException;
        #endregion

        #region [Method definitions]
        void WriteLine(string message, LogLevel level, [CallerMemberName] string caller = "");
        void WriteLines(List<string> messages, LogLevel level, [CallerMemberName] string caller = "");
        Task WriteLineAsync(string message, LogLevel level, [CallerMemberName] string caller = "");
        void PurgeOldLogs(int age = 90, string ext = "*.log");
        string GetCurrentLogPath();
        #endregion
    }

    /// <summary>
    /// Logger implementation.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string mLogRoot;
        private readonly string mAppName;
        private object threadLock = new object();
        public event Action<string, LogLevel> OnLogMessage = (message, level) => { };
        public event Action<string> OnPurgeComplete = (message) => { };
        public event Action<Exception> OnException = (ex) => { };
        public LogLevel logLevel { get; set; }

        #region [Constructors]
        public FileLogger(string logRoot)
        {
            logLevel = LogLevel.Debug;
            mLogRoot = logRoot;
            mAppName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        }

        public FileLogger(string logPath, LogLevel level)
        {
            mLogRoot = logPath;
            logLevel = level;
            mAppName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        }
        #endregion

        #region [Method implementation]
        /// <summary>
        /// Use for writing single line of data (synchronous).
        /// </summary>
        public void WriteLine(string message, LogLevel level, [CallerMemberName] string caller = "")
        {
            if (level < logLevel || level == LogLevel.Off) { return; }

            lock (threadLock)
            {
                string fullPath = GetCurrentLogPath();

                if (!IsPathTooLong(fullPath))
                {
                    var directory = Path.GetDirectoryName(fullPath);
                    try { Directory.CreateDirectory(directory); }
                    catch (Exception) { OnException?.Invoke(new Exception($"Could not create directory '{directory}'")); }

                    try
                    {
#if DEBUG
                        Debug.WriteLine($"{message}", $"{caller}");
#endif
                        using (var fileStream = new StreamWriter(File.OpenWrite(fullPath)))
                        {
                            // Jump to the end of the file before writing (same as append).
                            fileStream.BaseStream.Seek(0, SeekOrigin.End);
                            // Write the text to the file (adds CRLF natively).
                            fileStream.WriteLine("[{0}]\t{1}\t{2}\t{3}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt"), level, string.IsNullOrEmpty(caller) ? "N/A" : caller, message);
                        }
                        OnLogMessage?.Invoke(message, level);
                    }
                    catch (Exception ex) { OnException?.Invoke(ex); }
                }
                else { OnException?.Invoke(new Exception($"Path too long: {fullPath}")); }
            }
        }

        /// <summary>
        /// Use for writing large amounts of data at once (synchronous).
        /// </summary>
        public void WriteLines(List<string> messages, LogLevel level, [CallerMemberName] string caller = "")
        {
            if (level < logLevel || level == LogLevel.Off || messages.Count == 0) { return; }

            lock (threadLock)
            {
                string fullPath = GetCurrentLogPath();

                var directory = Path.GetDirectoryName(fullPath);
                try { Directory.CreateDirectory(directory); }
                catch (Exception) { OnException?.Invoke(new Exception($"Could not create directory '{directory}'")); }

                if (!IsPathTooLong(fullPath))
                {
                    try
                    {
                        using (var fileStream = new StreamWriter(File.OpenWrite(fullPath)))
                        {
                            // Jump to the end of the file before writing (same as append).
                            fileStream.BaseStream.Seek(0, SeekOrigin.End);
                            foreach (var message in messages)
                            {
                                // Write the text to the file (adds CRLF natively).
                                fileStream.WriteLine("[{0}]\t{1}\t{2}\t{3}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt"), level, string.IsNullOrEmpty(caller) ? "N/A" : caller, message);
                            }
                        }
                        OnLogMessage?.Invoke($"wrote {messages.Count} lines", level);
                    }
                    catch (Exception ex) { OnException?.Invoke(ex); }
                }
                else { OnException?.Invoke(new Exception($"Path too long: {fullPath}")); }
            }
        }

        /// <summary>
        /// Use for writing single line of data (asynchronous).
        /// </summary>
        public async Task WriteLineAsync(string message, LogLevel level, [CallerMemberName] string caller = "")
        {
            if (level < logLevel || level == LogLevel.Off) { return; }

            string fullPath = GetCurrentLogPath();

            var directory = Path.GetDirectoryName(fullPath);
            try { Directory.CreateDirectory(directory); }
            catch (Exception) { OnException?.Invoke(new Exception($"Could not create directory '{directory}'")); }

            if (!IsPathTooLong(fullPath))
            {
                try
                {
#if DEBUG
					Debug.WriteLine($"{message}", $"{caller}");
#endif
					using (var fileStream = new StreamWriter(File.OpenWrite(fullPath)))
                    {
                        // Jump to the end of the file before writing (same as append).
                        fileStream.BaseStream.Seek(0, SeekOrigin.End);
                        // Write the text to the file (adds CRLF natively).
                        await fileStream.WriteLineAsync(string.Format("[{0}]\t{1}\t{2}\t{3}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt"), level, string.IsNullOrEmpty(caller) ? "N/A" : caller, message));
                    }
                    OnLogMessage?.Invoke(message, level);
                }
                catch (Exception ex) { OnException?.Invoke(ex); }
            }
            else { OnException?.Invoke(new Exception($"Path too long: {fullPath}")); }
        }

        /// <summary>
        /// Use for writing large amounts of data at once (asynchronous).
        /// </summary>
        public async Task WriteLinesAsync(List<string> messages, LogLevel level, [CallerMemberName] string caller = "")
        {
            if (level < logLevel || level == LogLevel.Off || messages.Count == 0) { return; }

            string fullPath = GetCurrentLogPath();

            var directory = Path.GetDirectoryName(fullPath);
            try { Directory.CreateDirectory(directory); }
            catch (Exception) { OnException?.Invoke(new Exception($"Could not create directory '{directory}'")); }

            if (!IsPathTooLong(fullPath))
            {
                try
                {
                    using (var fileStream = new StreamWriter(File.OpenWrite(fullPath)))
                    {
                        // Jump to the end of the file before writing (same as append).
                        fileStream.BaseStream.Seek(0, SeekOrigin.End);
                        foreach (var message in messages)
                        {
                            // Write the text to the file (adds CRLF natively).
                            await fileStream.WriteLineAsync(string.Format("[{0}]\t{1}\t{2}\t{3}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt"), level, string.IsNullOrEmpty(caller) ? "N/A" : caller, message));
                        }
                    }
                    OnLogMessage?.Invoke($"wrote {messages.Count} lines", level);
                }
                catch (Exception ex) { OnException?.Invoke(ex); }
            }
            else { OnException?.Invoke(new Exception($"Path too long: {fullPath}")); }
        }

        /// <summary>
        /// Get rid of older log files.
        /// </summary>
        /// <param name="age">age of oldest files to remove (in days)</param>
        /// <param name="ext">file extension to look for</param>
        public void PurgeOldLogs(int age = 90, string ext = "*.log")
        {
            //ThreadPool.QueueUserWorkItem((object o) =>
            //{
            //    PerformPurge($@"{mLogRoot}\{mAppName}", age, ext);
            //    OnPurgeComplete?.Invoke($"Old log purge complete");
            //});

            new Thread(() =>
            {
                PerformPurge($@"{mLogRoot}\{mAppName}", age, ext);
                OnPurgeComplete?.Invoke($"Old log purge complete");
            })
            { IsBackground = true, Name = "PurgeLogs", Priority = ThreadPriority.Lowest }.Start();
        }

        /// <summary>
        /// This method should be called by a lower thread.
        /// </summary>
        void PerformPurge(string location, int age = 90, string ext = "*.log")
        {
            try
            {
                string appLogs = Path.Combine(mLogRoot, mAppName);

                if (string.IsNullOrEmpty(appLogs))
                {
                    appLogs = Path.Combine(GetRoot(), "Logs");
                }

                string lastFilePath = "";
                string[] logFiles = Directory.GetFiles(appLogs, ext, SearchOption.AllDirectories);
                // Only remove 50K files per check.
                IEnumerable<string> topFiles = logFiles.OrderBy(files => files).Take(50000);
                foreach (string fn in topFiles)
                {
                    if (IsPathTooLong(fn))
                        continue;

                    lastFilePath = fn;
                    DateTime dtOfLog = System.IO.File.GetCreationTime(fn);
                    if ((DateTime.Now - dtOfLog).TotalDays > age)
                    {
                        try { File.Delete(fn); }
                        catch (Exception ex) { OnException?.Invoke(new Exception($"DeleteFile: {ex.Message}")); }
                    }
                    Thread.Sleep(1); // Relax this loop.
                }

                if (!string.IsNullOrEmpty(lastFilePath))
                {   // Is the folder empty now?
                    if (Directory.GetFiles(Path.GetDirectoryName(lastFilePath)).Length < 1) // Remove folder if no more files exist.
                    {
                        try
                        {
                            WriteLine($"Deleting directory {Path.GetDirectoryName(lastFilePath)}", LogLevel.Debug);
                            Directory.Delete(Path.GetDirectoryName(lastFilePath), true);
                        }
                        catch (Exception ex) { OnException?.Invoke(new Exception($"DeleteFolder: {ex.Message}")); }
                    }
                }
            }
            catch (Exception ex)
            {
                OnException?.Invoke(new Exception($"PurgeOldLogs: {ex.Message}"));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] // Prevent the JIT compiler from inlining the method that calls us.
        public static List<string> GetCallerInfo()
        {
            List<string> frames = new List<string>();
            StackTrace stackTrace = new StackTrace();

            var fm1 = stackTrace.GetFrame(1)?.GetMethod();
            var fm2 = stackTrace.GetFrame(2)?.GetMethod();
            var fm3 = stackTrace.GetFrame(3)?.GetMethod();
            frames.Add($"[Method1]: {fm1?.Name}  [Class1]: {fm1?.DeclaringType?.Name}");
            frames.Add($"[Method2]: {fm2?.Name}  [Class2]: {fm2?.DeclaringType?.Name}");
            frames.Add($"[Method3]: {fm3?.Name}  [Class3]: {fm3?.DeclaringType?.Name}");
            return frames;
        }
        #endregion

        #region [Path helpers]
        public string GetCurrentLogPath() => Path.Combine(mLogRoot, $@"{mAppName}\{DateTime.Today.Year}\{DateTime.Today.Month.ToString("00")}-{DateTime.Today.ToString("MMMM")}\{mAppName}_{DateTime.Now.ToString("dd")}.log");

        /// <summary>
        /// Gets usable drive from <see cref="DriveType.Fixed"/> volumes.
        /// </summary>
        public static string GetRoot(bool descendingOrder = true)
        {
            string root = string.Empty;
            try
            {
                if (App.IsPackaged)
                {
                    root = ApplicationData.Current.LocalFolder.Path;
                }
                else
                {
                    if (!descendingOrder)
                    {
                        var logPaths = DriveInfo.GetDrives().Where(di => (di.DriveType == DriveType.Fixed) && (di.IsReady) && (di.AvailableFreeSpace > 1000000)).Select(di => di.RootDirectory).OrderBy(di => di.FullName);
                        root = logPaths.FirstOrDefault().FullName;
                    }
                    else
                    {
                        var logPaths = DriveInfo.GetDrives().Where(di => (di.DriveType == DriveType.Fixed) && (di.IsReady) && (di.AvailableFreeSpace > 1000000)).Select(di => di.RootDirectory).OrderByDescending(di => di.FullName);
                        root = logPaths.FirstOrDefault().FullName;
                    }
                }
                return root;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRoot: {ex.Message}");
                return root;
            }
        }

        public static string GetLastFixedDrive()
        {
            char lastLetter = 'C';
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    if (drive.Name[0] > lastLetter)
                        lastLetter = drive.Name[0];
                }
            }
            return $"{lastLetter}:";
        }

        static bool IsValidPath(string path)
        {
            if ((File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) == System.IO.FileAttributes.ReparsePoint)
            {
                Debug.WriteLine("'" + path + "' is a reparse point (skipped)");
                return false;
            }
            if (!IsReadable(path))
            {
                Debug.WriteLine("'" + path + "' *ACCESS DENIED* (skipped)");
                return false;
            }
            return true;
        }

        static bool IsReadable(string path)
        {
            try
            {
                var dn = Path.GetDirectoryName(path);
                string[] test = Directory.GetDirectories(dn, "*.*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            return true;
        }

        static bool IsPathTooLong(string path)
        {
            try
            {
                var tmp = Path.GetFullPath(path);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return true;
            }
        }
        #endregion
    }
}
