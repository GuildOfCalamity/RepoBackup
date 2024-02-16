#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Search;

#region [For Acrylic Brush Effect]
using Microsoft.UI.Xaml;
using Microsoft.UI.Composition.SystemBackdrops;
using WinRT; // required to support Window.As<ICompositionSupportsSystemBackdrop>()
#endregion

using WinUIDemo.Models;

namespace WinUIDemo;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    ValueStopwatch _elapsed = new ValueStopwatch();
    public FileLogger? Logger { get; private set; }
    public SettingsManager AppSettings { get; private set; }
    public static Windows.UI.Color BackdropColor { get; private set; }

	/// <summary>
	/// Primary Constructor
	/// </summary>
	public MainWindow()
    {
        Title = App.GetCurrentAssemblyName()?.Split(',')[0];
        this.InitializeComponent();

        // These must come after InitializeComponent() if using a NavigationView. 
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(CustomTitleBar);
        Logger = App.GetService<FileLogger>();
        AppSettings = App.GetService<SettingsManager>() ?? new SettingsManager();

        // Sample some of the Windows.UI.ViewManagement.UISettings:
        Debug.WriteLine($"{nameof(App.TextScaleFactor)} => {App.TextScaleFactor}");
        Debug.WriteLine($"{nameof(App.AnimationsEffectsEnabled)} => {App.AnimationsEffectsEnabled}");
        Debug.WriteLine($"{nameof(App.TransparencyEffectsEnabled)} => {App.TransparencyEffectsEnabled}");
        Debug.WriteLine($"{nameof(App.AutoHideScrollbars)} => {App.AutoHideScrollbars}");

        // Invoke the DesktopAcrylicController
        _ = TrySetAcrylicBackdrop();
    }

	/// <summary>
	/// Secondary Constructor
	/// </summary>
	public MainWindow(Dictionary<string, string> args) : this()
    {
        Logger?.WriteLine($"Received arguments: {args.ToString<string,string>()}", LogLevel.Debug);
        if (args.TryGetValue("-mode", out string? val) && !string.IsNullOrEmpty(val) && val.Contains("test"))
        {
            // Testing CacheItem
            TestCache(null);

            // Testing Windows.Storage.FileProperties
            TestStorageFileProperties(null);

            // Testing Windows.Storage.Search
            TestStorageFolderQuery();

            // Testing generic queue
            TestSyncQueue();

            // Testing hash options
            TestHashes();

			// Testing PackageManagerHelper
			TestPackageListing();

            _randomizeColor = true;
		}
    }

	#region [For Acrylic Brush Effect]
    bool _randomizeColor = false;
	bool _attemptedBackdrop = false;
	WindowsSystemDispatcherQueueHelper? _wsdqHelper;
	DesktopAcrylicController? _acrylicController;
	MicaController? _micaController; // Win11 only
	SystemBackdropConfiguration? _configurationSource;
	#endregion

	#region [Acrylic Brush Background]
	/// <summary>
	/// Do not call more than once!
	/// https://stackoverflow.com/questions/76535706/easiest-way-to-set-the-window-background-to-an-acrylic-brush-in-winui3/76536129#76536129
	/// </summary>
	/// <returns>true if DesktopAcrylicController is supported, false otherwise</returns>
	bool TrySetAcrylicBackdrop()
    {
        if (_attemptedBackdrop)
            return _attemptedBackdrop;

        _attemptedBackdrop = true;

        if (DesktopAcrylicController.IsSupported())
        {
            _wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            _wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

            // Hooking up the policy object
            _configurationSource = new SystemBackdropConfiguration();
            this.Activated += WindowOnActivated;
            this.Closed += WindowOnClosed;
            ((FrameworkElement)this.Content).ActualThemeChanged += WindowOnThemeChanged;

            // Initial configuration state.
            _configurationSource.IsInputActive = true;
            SetConfigurationSourceTheme();

            if (AppSettings.Config.RandomBackdrop)
			    BackdropColor = Extensions.GetRandomMicrosoftUIColor();
            else
                BackdropColor = Microsoft.UI.Colors.Navy;

            if (App.WindowsVersion.Major >= 11) // Windows 11
            {
                _micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();
                _micaController.TintOpacity = 0.2f; // May be too bright against a light background.
                _micaController.LuminosityOpacity = 0.1f;
                //_micaController.TintColor = Microsoft.UI.Colors.Navy;
                _micaController.TintColor = BackdropColor;
				// Fallback color is only used when the window state becomes deactivated.
				_micaController.FallbackColor = Microsoft.UI.Colors.Transparent;
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                _micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                _micaController.SetSystemBackdropConfiguration(_configurationSource);
            }
            else // Windows 10
            {
                // Enable the system backdrop.
                _acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();
                _acrylicController.TintOpacity = 0.2f; // May be too bright against a light background.
                _acrylicController.LuminosityOpacity = 0.1f;
				//_acrylicController.TintColor = Microsoft.UI.Colors.Navy;
                _acrylicController.TintColor = BackdropColor;
                // Fallback color is only used when the window state becomes deactivated.
				_acrylicController.FallbackColor = Microsoft.UI.Colors.Transparent;
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
				_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
            }
            return true; // succeeded
        }

        Logger?.WriteLine($"DesktopAcrylicController is not supported", LogLevel.Warning);
        return false; // Acrylic is not supported on this system
    }

    void WindowOnActivated(object sender, WindowActivatedEventArgs args)
    {
        Debug.WriteLine($"WindowActivationState is now {args.WindowActivationState}");

        if (_configurationSource is not null)
        {
            _configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;

			#region [If you wanted to randomize the color on each activation]
            var newColor = Extensions.GetRandomMicrosoftUIColor();

			if (App.WindowsVersion.Major >= 11) // Windows 11
            {
                if (_micaController is not null && _randomizeColor)
                {
                    Debug.WriteLine($"New color will be {newColor}");
                    _micaController.TintColor = newColor;
                    _micaController.FallbackColor = Microsoft.UI.Colors.Transparent;
				}
            }
            else // Windows 10
            {
                if (_acrylicController is not null && _randomizeColor)
                {
                    Debug.WriteLine($"New color will be {newColor}");
					_acrylicController.TintColor = newColor;
                    _acrylicController.FallbackColor = Microsoft.UI.Colors.Transparent;
				}
            }
			#endregion
		}
	}

    void WindowOnClosed(object sender, WindowEventArgs args)
    {
        // Make sure any Mica/Acrylic controller is disposed
        // so it doesn't try to use this closed window.
        if (App.WindowsVersion.Major >= 11) // Windows 11
        {
            if (_micaController is not null)
            {
                _micaController.Dispose();
                _micaController = null;
            }
        }
        else // Windows 10
        {
            if (_acrylicController is not null)
            {
                _acrylicController.Dispose();
                _acrylicController = null;
            }
        }

        this.Activated -= WindowOnActivated;
        _configurationSource = null;
    }

    void WindowOnThemeChanged(FrameworkElement sender, object args)
    {
        if (_configurationSource is not null)
        {
            SetConfigurationSourceTheme();
        }
    }

    void SetConfigurationSourceTheme()
    {
        if (_configurationSource is not null)
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark:
                    _configurationSource.Theme = SystemBackdropTheme.Dark;
                    break;
                case ElementTheme.Light:
                    _configurationSource.Theme = SystemBackdropTheme.Light;
                    break;
                case ElementTheme.Default:
                    _configurationSource.Theme = SystemBackdropTheme.Default;
                    break;
            }
        }
    }

    /// <summary>
    /// My poor man's version. This does not allow the main window to be translucent.
    /// In the MainWindow constructor: this.Activated += MainWindowOnActivated;
    /// </summary>
    void MainWindowOnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Configure the AcrylicBrush.
        _acrylicBrush.TintTransitionDuration = TimeSpan.FromSeconds(2);
        _acrylicBrush.TintOpacity = 0.15;
        _acrylicBrush.TintLuminosityOpacity = 0.1;
        _acrylicBrush.TintColor = Extensions.GetRandomMicrosoftUIColor();
	    // Fallback color is only used when the window state becomes deactivated.
		_acrylicBrush.FallbackColor = Microsoft.UI.Colors.Transparent;

        // Set the AcrylicBrush as the background of the window.
        this.Root.Background = _acrylicBrush;
    }
    static AcrylicBrush _acrylicBrush = new AcrylicBrush();
    #endregion

    #region [Helpers]
    void ChangeCursorIfUWP()
    {
        if (Window.Current.CoreWindow != null)
        {
            Windows.UI.Core.CoreCursor currentCursor = Window.Current.CoreWindow.PointerCursor;
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Wait, 10);
        }
    }

    async void ShowDialog(string message)
    {
        var root = this.Content as FrameworkElement;
        await root?.MessageDialogAsync("Notice", message);
    }
	#endregion

	#region [Superfluous Tests]
	/// <summary>
	/// Uses the <see cref="Windows.Management.Deployment.PackageManager"/>
    /// to walk through all installed packages on the local machine.
	/// </summary>
	void TestPackageListing()
    {
		Task.Run(delegate () 
        { 
            PackageManagerHelper.IteratePackages();

        }).ContinueWith(t =>
		{
		    Logger?.WriteLine($"{nameof(PackageManagerHelper)} test complete", LogLevel.Debug);
		});
    }

	/// <summary>
	/// Testing method for <see cref="CacheItem{T}"/>.
	/// </summary>
	/// <param name="folder">directory path</param>
	/// <param name="arbitraryWait">millisecond amount</param>
	/// <remarks>
	/// If this was a memory-based, long-running service/application we 
	/// could use this to keep track of the last time we examined a folder.
	/// This could also be used for login/session tracking.
	/// </remarks>
	void TestCache(string? folder, int arbitraryWait = 60001)
    {
        if (string.IsNullOrEmpty(folder))
        {
            if (App.IsPackaged)
                folder = ApplicationData.Current.LocalFolder.Path;
            else
                folder = StorageFolder.GetFolderFromPathAsync(System.AppContext.BaseDirectory).GetAwaiter().GetResult().Path;
        }

        List<CacheItem<DirectoryInfo>> items = new();

        Task.Run(async delegate ()
        {
            var target = folder ?? Directory.GetCurrentDirectory();
            string[] dirs = System.IO.Directory.GetDirectories(target);
            foreach (string dir in dirs)
            {
                var di = new DirectoryInfo(dir);
                items.Add(new CacheItem<DirectoryInfo>(dir, di.LastAccessTime, di));
            }
            
            // Adding some arbitrary value to test age.
            await Task.Delay(arbitraryWait);

        }).ContinueWith(t =>
        {
            foreach (var item in items)
            {
                var elapse = DateTime.Now - item.Updated;
                Logger?.WriteLine($"Age of '{item.Id}' is {elapse.ToReadableTime()}", LogLevel.Debug);
            }
        });
    }

    /// <summary>
    /// Testing method for <see cref="Windows.Storage.Search.StorageFileQueryResult"/>.
    /// </summary>
    /// <param name="cfq"><see cref="CommonFileQuery"/></param>
    void TestStorageFolderQuery(CommonFileQuery cfq = CommonFileQuery.DefaultQuery)
    {
        //Task.Run(async delegate () {
        //    try {
        //        StorageFolder? localFolder = ApplicationData.Current.LocalFolder;
        //        StorageFile? file = await localFolder.CreateFileAsync("Settings.xml", CreationCollisionOption.OpenIfExists);
        //        StorageApplicationPermissions.MostRecentlyUsedList.Add(file, "", RecentStorageItemVisibility.AppAndSystem);
        //    }
        //    catch (Exception) { }
        //});

        Task.Run(async delegate ()
        {
            try
            {
                List<string> filter = new List<string>();
                filter.Add(".dll"); filter.Add(".exe"); filter.Add(".pri");
                filter.Add(".xbf"); filter.Add(".jpg"); filter.Add(".png");
                filter.Add(".json"); filter.Add(".xml"); filter.Add(".mui");

                // Setup the query.
                QueryOptions queryOptions = new QueryOptions(cfq, filter);
                queryOptions.FolderDepth = FolderDepth.Deep;
                queryOptions.SortOrder.Clear();
                if (cfq == CommonFileQuery.DefaultQuery)
                {
                    SortEntry seAccess = new SortEntry();
                    seAccess.PropertyName = "System.DateAccessed"; // https://learn.microsoft.com/en-us/windows/win32/properties/props-system-dateaccessed
                    seAccess.AscendingOrder = false;

                    SortEntry seDate = new SortEntry();
                    seDate.PropertyName = "System.DateModified"; // https://learn.microsoft.com/en-us/windows/win32/properties/props-system-datemodified
                    seDate.AscendingOrder = false;

                    queryOptions.SortOrder.Add(seDate);
                    //queryOptions.SortOrder.Add(seAccess);
                }

#if IS_UNPACKAGED
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(System.AppContext.BaseDirectory);
#else
                StorageFolder folder = ApplicationData.Current.LocalFolder;
#endif

                StorageFileQueryResult queryResult = folder.CreateFileQueryWithOptions(queryOptions);

                var count = await queryResult.GetItemCountAsync();
                if (count > 0)
                {
                    // This call can take a while based on how many files are in the folder(s).
                    IReadOnlyList<StorageFile> files = await queryResult.GetFilesAsync();
                    /*
                    foreach (var file in files)
                    {
                        var basicProps = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
                        var trueModDate = basicProps != null ? basicProps.DateModified.DateTime : DateTime.Now;
                        Debug.WriteLine($"{file.DisplayName} ({trueModDate})");
                    }
                    */
                    Logger?.WriteLine($"Discovered {count} files.", LogLevel.Debug);
                }
                else
                {
                    Logger?.WriteLine($"No files match the query.", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger?.WriteLine($"StorageFolderQuery: {ex.Message}", LogLevel.Warning);
            }
        });
    }

    /// <summary>
    /// Testing method for <see cref="Windows.Storage.FileProperties.StorageItemContentProperties"/>.
    /// </summary>
    /// <param name="fullPath"></param>
    void TestStorageFileProperties(string? fullPath)
    {
        var sourcePath = fullPath ?? System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets\\AppIcon.ico");

        Task.Run(async delegate ()
        {
            var file = await StorageFile.GetFileFromPathAsync(sourcePath);
            if (file == null) { return; }

            List<string> _extendedProperties = new List<string>();
            // https://learn.microsoft.com/en-us/windows/win32/properties/props-system-dateaccessed
            _extendedProperties.Add("System.DateAcquired");            // The acquisition date of the file or media. This property is related to a particular user or group of users. For example, this data is used as the main sorting axis for the virtual folder New Music, which enables people to browse the latest additions to their collection.
            _extendedProperties.Add("System.DateAccessed");            // Indicates the last time the item was accessed: 6/1/2023 3:07:16 PM -04:00
            _extendedProperties.Add("System.DateCreated");             // The date and time the item was created on the file system where it is currently located. This property is automatically promoted by the file system: 5/11/2023 3:16:04 PM -04:00
            _extendedProperties.Add("System.DateImported");            // The date and time the file was imported into a private application database. For example, this property can be used when a photo is imported into a photo database: 3/29/2011 3:32:06 AM -04:00
            _extendedProperties.Add("System.DateModified");            // The date and time of the last modification to the item: 3/29/2011 3:32:06 AM -04:00
            _extendedProperties.Add("System.FileAllocationSize");      // Equivalent to size on disk: 147456
            _extendedProperties.Add("System.FileAttributes");          // ReadOnly, Archive, Normal, Directory, Temporary, Incomplete
            _extendedProperties.Add("System.FileExtension");           // The file's extension.
            _extendedProperties.Add("System.FileFRN");                 // This is the unique file ID, also known as the FileReferenceNumber. For a given file, this is the same value as is found in the structure variable FILE_ID_BOTH_DIR_INFO.FileId, via GetFileInformationByHandleEx().
            _extendedProperties.Add("System.FileName");                // AppIcon.ico
            _extendedProperties.Add("System.FileOwner");               // Domain\User
            _extendedProperties.Add("System.FilePlaceholderStatus");   // FileAccessible, PrimaryStreamAvailable, MarkedForOffline, CloudFile
            _extendedProperties.Add("System.FreeSpace");               // Free space of the drive where the file resides (in total bytes).
            _extendedProperties.Add("System.IsPinnedToNameSpaceTree"); // Identifies whether a shell folder is pinned to the navigation pane: True
            _extendedProperties.Add("System.IsSendToTarget");          // Provided by certain shell folders. Return TRUE if the folder is a valid SendTo target.
            _extendedProperties.Add("System.IsShared");                // False
            _extendedProperties.Add("System.ContentType");             // image/x-icon
            _extendedProperties.Add("System.ComputerName");            // The name of the computer where the item or file is located.
            _extendedProperties.Add("System.ItemDate");                // The primary date of interest for an item. In the case of photos, for example, this property maps to System.Photo.DateTaken: 3/29/2011 3:32:06 AM -04:00
            _extendedProperties.Add("System.ItemFolderNameDisplay");   // Name of the folder the file resides in: Assets
            _extendedProperties.Add("System.ItemFolderPathDisplay");   // Similar to full path without file name and extension.
            _extendedProperties.Add("System.ItemFolderPathDisplayNarrow"); // Formatted pathing string.
            _extendedProperties.Add("System.ItemName");                // AppIcon.ico
            _extendedProperties.Add("System.ItemNameDisplay");         // AppIcon.ico
            _extendedProperties.Add("System.ItemNameDisplayWithoutExtension"); // Formatted pathing string.
            _extendedProperties.Add("System.ItemPathDisplay");         // Similar to full path with file name and extension.
            _extendedProperties.Add("System.ItemPathDisplayNarrow");   // Formatted pathing string.
            _extendedProperties.Add("System.ItemType");                // Will be the file's extension: .ico
            _extendedProperties.Add("System.ItemTypeText");            // Full name of the extension type: Icon
            _extendedProperties.Add("System.VolumeId");                // Storage device's ID: 83717388-0000-0000-0000-300300000000
            _extendedProperties.Add("System.ThumbnailCacheId");        // A unique value used as a key to cache thumbnails. The value changes when the name, volume, or data modified of an item changes: 703880385179307744
            _extendedProperties.Add("System.ZoneIdentifier");          // 0: You can use "dir /r" to list alternate data streams for each file. See NativeMethods.ZONE_ID for details.

            // Get extended properties.
            IDictionary<string, object> extraProps = await file.Properties.RetrievePropertiesAsync(_extendedProperties).AsTask().ConfigureAwait(false);
            if (extraProps.Count > 0)
            {
                string formatted = "";
                foreach (var prop in extraProps)
                {
                    try
                    {
                        if (prop.Key.StartsWith("System.ZoneIdentifier"))
                        {
                            switch ((UInt32)prop.Value)
                            {
                                case (UInt32)NativeMethods.ZONE_ID.LocalMachine:
                                    formatted = $"{prop.Key}: Local machine";
                                    break;
                                case (UInt32)NativeMethods.ZONE_ID.LocalIntranet:
                                    formatted = $"{prop.Key}: Local intranet";
                                    break;
                                case (UInt32)NativeMethods.ZONE_ID.TrustedSites:
                                    formatted = $"{prop.Key}: Trusted sites";
                                    break;
                                case (UInt32)NativeMethods.ZONE_ID.Internet:
                                    formatted = $"{prop.Key}: Internet";
                                    // You can remove the Zone=3 by calling the Kernel32 helper...
                                    _ = NativeMethods.DeleteFile(sourcePath);
                                    break;
                                case (UInt32)NativeMethods.ZONE_ID.RestrictedSites:
                                    formatted = $"{prop.Key}: Restricted sites";
                                    break;
                                default:
                                    formatted = $"{prop.Key}: Undefined";
                                    break;
                            }
                        }
                        else if (prop.Key.StartsWith("System.FileAttributes"))
                        {
                            bool isReadOnly = (((Windows.Storage.FileAttributes)prop.Value & Windows.Storage.FileAttributes.ReadOnly) == Windows.Storage.FileAttributes.ReadOnly);
                            bool isTemporary = (((Windows.Storage.FileAttributes)prop.Value & Windows.Storage.FileAttributes.Temporary) == Windows.Storage.FileAttributes.Temporary);
                            bool isArchive = (((Windows.Storage.FileAttributes)prop.Value & Windows.Storage.FileAttributes.Archive) == Windows.Storage.FileAttributes.Archive);
                            bool isDirectory = (((Windows.Storage.FileAttributes)prop.Value & Windows.Storage.FileAttributes.Directory) == Windows.Storage.FileAttributes.Directory);
                            bool isNormal = (((Windows.Storage.FileAttributes)prop.Value & Windows.Storage.FileAttributes.Normal) == Windows.Storage.FileAttributes.Normal);
                            bool isIncomplete = (((Windows.Storage.FileAttributes)prop.Value & Windows.Storage.FileAttributes.LocallyIncomplete) == Windows.Storage.FileAttributes.LocallyIncomplete);

                            formatted = $"{prop.Key}: ReadOnly={isReadOnly}, Archive={isArchive}, Normal={isNormal}, Directory={isDirectory}, Temporary={isTemporary}, Incomplete={isIncomplete}";
                        }
                        else if (prop.Key.StartsWith("System.FilePlaceholderStatus"))
                        {
                            bool isFileAccessible = ((NativeMethods.PLACEHOLDER_STATES)prop.Value & NativeMethods.PLACEHOLDER_STATES.CREATE_FILE_ACCESSIBLE) == NativeMethods.PLACEHOLDER_STATES.CREATE_FILE_ACCESSIBLE;
                            bool isPrimaryStream = ((NativeMethods.PLACEHOLDER_STATES)prop.Value & NativeMethods.PLACEHOLDER_STATES.FULL_PRIMARY_STREAM_AVAILABLE) == NativeMethods.PLACEHOLDER_STATES.FULL_PRIMARY_STREAM_AVAILABLE;
                            bool isMarkedOffline = ((NativeMethods.PLACEHOLDER_STATES)prop.Value & NativeMethods.PLACEHOLDER_STATES.MARKED_FOR_OFFLINE_AVAILABILITY) == NativeMethods.PLACEHOLDER_STATES.MARKED_FOR_OFFLINE_AVAILABILITY;
                            bool isCloudFile = ((NativeMethods.PLACEHOLDER_STATES)prop.Value & NativeMethods.PLACEHOLDER_STATES.CLOUDFILE_PLACEHOLDER) == NativeMethods.PLACEHOLDER_STATES.CLOUDFILE_PLACEHOLDER;

                            formatted = $"{prop.Key}: FileAccessible={isFileAccessible}, PrimaryStreamAvailable={isPrimaryStream}, MarkedForOffline={isMarkedOffline}, CloudFile={isCloudFile}";
                        }
                        else if (prop.Key.StartsWith("System.DateModified"))
                        {
                            Debug.WriteLine($"{prop.Key}: {prop.Value}");
                            // https://github.com/microsoft/WindowsAppSDK/issues/3659
                            // NOTE: Do not use System.DateModified for an accurate file modified date,
                            // use the GetBasicProperties method instead, or "new System.IO.FileInfo(sourcePath).LastWriteTime"
                            var basicProps = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
                            var trueModDate = basicProps != null ? basicProps.DateModified.DateTime : DateTime.Now;
                            formatted = $"{prop.Key}: {trueModDate}";
                        }
                        else
                        {
                            formatted = $"{prop.Key}: {prop.Value}";
                        }
                        Logger?.WriteLine($"{formatted}", LogLevel.Debug);
                    }
                    catch (Exception ex)
                    {
                        Logger?.WriteLine($"ForEach: {ex.Message}", LogLevel.Warning);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Testing method for hashing extensions.
    /// </summary>
    /// <remarks>All results were within the margin of error.</remarks>
    void TestHashes(int iterations = 20000, int total = 25)
    {
        Task.Run(delegate ()
        {
            string result = "";
            double milliTotal = 0D;
            for (int j = 0; j < total; j++)
            {
                _elapsed = ValueStopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    //result = $"{Extensions.BasicHash(Extensions.KeyGen(32))}";      // => 71
                    //result = $"{LowCityHash.Hash64(Extensions.KeyGen(32))}";        // => 72
                    //result = $"{CRC32.Compute(Extensions.KeyGen(32))}";             // => 72
                    result = $"{Extensions.FNV2Hash(Extensions.KeyGen(32))}";         // => 70
                    //result = $"{Extensions.FNV2HashUnsafe(Extensions.KeyGen(32))}"; // => 69
                }
                milliTotal += _elapsed.GetElapsedTime().TotalMilliseconds;
            }
            Logger?.WriteLine($"Hashing lasted {milliTotal / (double)total:N1} ms (avg)", LogLevel.Debug);
        });
    }

    /// <summary>
    /// A producer/consumer test for files to copy in a queue.
    /// </summary>
    void TestSyncQueue(int arbitraryWait = 10000)
    {
        // We want the starting conditions to be unbalanced.
        int setMax = 45;
        int getMax = 50;

        // We're setting up a situation where one process is sending us information
        // and the other is processing that information (producer/consumer).
        SynchronizedQueue<string> sharedGeneric = new();
        sharedGeneric.Maximum = 20;

        // Setup the empty event.
        sharedGeneric.BufferEmptyEvent += (s, e) =>
        {
            var eTime = (DateTime?)e?.FirstOrDefault().Key;
            Console.WriteLine($"[{eTime}] {e?.FirstOrDefault().Value} ");
        };
        // Setup the full event.
        sharedGeneric.BufferFullEvent += (s, e) =>
        {
            var eTime = (DateTime?)e?.FirstOrDefault().Key;
            Console.WriteLine($"[{eTime}] {e?.FirstOrDefault().Value} ");
        };

        sharedGeneric.EnabledChanging += new EventHandler<PropertyChangingEventArgs<bool>>(EnabledChanging);
        sharedGeneric.EnabledChanged += new EventHandler(EnabledChanged);

        var tSet = new Thread(() => { // We want this one to be faster so we can observe the throttling.
            SynchronizedQueueSet<string>(sharedGeneric, ref setMax, 0);
        })
        { IsBackground = true, Name = $"SetterGeneric", Priority = ThreadPriority.Lowest };

        var tGet = new Thread(() => { // We want this one to be slower so we can observe the throttling.
            SynchronizedQueueGet<string>(sharedGeneric, ref getMax, 0);
        })
        { IsBackground = true, Name = $"GetterGeneric", Priority = ThreadPriority.Lowest };

        Task.Run(async delegate ()
        {
            // Make 'em go…
            tSet.Start(); tGet.Start();

            // Simulate some time passing…
            await Task.Delay(arbitraryWait);

            // Signal the loops to finish up…
            sharedGeneric.Enabled = false;
            await Task.Delay(150);

        }).ContinueWith(t =>
        {
            Debug.WriteLine($"> {tGet.Name} state={tGet.ThreadState}");
            Debug.WriteLine($"> {tSet.Name} state={tSet.ThreadState}");
            Debug.WriteLine($"> SetterCount: {syncCntr}");
        });
    }

    #region [Generic Synchronize Methods]
    static int syncCntr = 0;
    static void EnabledChanging(object? sender, PropertyChangingEventArgs<bool> e)
    {
        Debug.WriteLine("> [Enabled] property changing to {0}", e.ProposedValue);
    }
    static void EnabledChanged(object? sender, EventArgs e)
    {
        if (sender != null)
            Debug.WriteLine("> [Enabled] property changed to {0}", ((SynchronizedQueue<string>)(sender)).Enabled);
    }
    static void SynchronizedQueueSet<T>(IQueue<T> shared, ref int maxWait, double percentage = 0.9)
    {
        while (shared.Enabled)
        {
            // Writing to the property will activate a lock on the SynchronizedQueue object.
            shared.Buffer = (T)Convert.ChangeType($"{++syncCntr}", typeof(T));
            // Besides Convert.ChangeType(), the double cast can solve this.
            /*
            if (typeof(T) == typeof(string))
                shared.Buffer = (T)(object)$"{++syncCntr}";
            else if (typeof(T) == typeof(int))
                shared.Buffer = (T)(object)++syncCntr;
            else
                throw new TypeAccessException($"Unsupported type: {typeof(T)}");
            */

            // Auto-balance (only one of us needs to do this, either the get method or the set method)
            if (percentage > 0 && ((double)shared.Count >= ((double)shared.Maximum * percentage)))
                Thread.Sleep(maxWait * 2);
            else
                Thread.Sleep(Random.Shared.Next(maxWait));
        }
        Debug.WriteLine(">> {0}: thread \"{1}\" is finished.", System.Reflection.MethodBase.GetCurrentMethod()?.Name, Thread.CurrentThread.Name);
    }
    static void SynchronizedQueueGet<T>(IQueue<T> shared, ref int maxWait, double percentage = 0.9)
    {
        T? buffSample = default;
        while (shared.Enabled)
        {
            // Reading from the property will activate a lock on the SynchronizedQueue object.
            buffSample = shared.Buffer;
            /*
            if (typeof(T) == typeof(string))
                Debug.WriteLine($">> Received type of {buffSample.GetType().Name} ({buffSample.GetType().BaseType.Name})");
            else if (typeof(T) == typeof(int))
                Debug.WriteLine($">> Received type of {buffSample.GetType().Name} ({buffSample.GetType().BaseType.Name})");
            else
                throw new TypeAccessException($"Unsupported type: {typeof(T)}");
            */

            // Auto-balance (only one of us needs to do this, either the get method or the set method)
            if (percentage > 0 && ((double)shared.Count >= ((double)shared.Maximum * percentage)))
                Thread.Sleep(maxWait / 2);
            else
                Thread.Sleep(Random.Shared.Next(maxWait));
        }
        Debug.WriteLine(">> {0}: thread \"{1}\" is finished.", System.Reflection.MethodBase.GetCurrentMethod()?.Name, Thread.CurrentThread.Name);
    }
    #endregion

    #endregion
}
