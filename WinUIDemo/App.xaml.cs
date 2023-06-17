#nullable enable

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Diagnostics;
using Windows.System.Profile;
using Windows.UI.ViewManagement;

using WinUIDemo.ViewModels;

namespace WinUIDemo;

/// <summary>
/// Our main <see cref="Microsoft.UI.Xaml.Application"/> wrapper.
/// The purpose of this application was to not use any 3rd party libs, 
/// so you'll find methods such as the home-brew service getter et. al.
/// </summary>
public partial class App : Application
{
    #region [Properties]
    private Window? m_window;
    private static UISettings m_UISettings = new UISettings();
    private static EasClientDeviceInformation m_deviceInfo = new EasClientDeviceInformation();
    public static event Action<string> OnWindowClosing = (msg) => { };
    public static Dictionary<string, string> ArgDictionary = new Dictionary<string, string>();
    public static Dictionary<string, string> MachineAndUserVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public static Version WindowsVersion => Extensions.GetWindowsVersionUsingAnalyticsInfo();
    public static FrameworkElement? MainRoot { get; private set; } // For alert dialogs, et al.
    public static string? AppBase { get; private set; } // For pathing.
    public static string? MachineID { get; private set; } // For identifying the client machine.
    public static string? SessionID { get; private set; } // For attaching a unique ID to the life-cycle.
    public static IntPtr WindowHandle { get; private set; } // For file open dialogs, et al.
    public static bool IsClosing { get; private set; } // For signaling processes that are still running.
    public static bool IsMainInstance { get; private set; }
    public static Mutex? InstanceMutex { get; private set; }

    // NOTE: To test this as a Packaged application you must change the <WindowsPackageType> from "None" to "MSIX" in your csproj file.
    public static IObjectStorageHelper SettingsHelper = new ObjectStorageHelper(new SystemSerializer());

    public static ulong AvailableMemory
    {
        get
        {
#if IS_UNPACKAGED
            return 0;
#else
            return MemoryManager.AppMemoryUsageLimit; // Will typically report somewhere below the commit charge limit.
#endif
        }
    }

    #region [User preferences from Windows.UI.ViewManagement]
    // We won't configure backing fields for these as the user could adjust them during app lifetime.
    public static bool TransparencyEffectsEnabled 
    {
        get => m_UISettings.AdvancedEffectsEnabled;
    }
    public static bool AnimationsEffectsEnabled
    {
        get => m_UISettings.AnimationsEnabled;
    }
    public static bool AutoHideScrollbars
    {
        get
        {
            if (WindowsVersion.Major >= 10 && WindowsVersion.Build >= 18362)
                return m_UISettings.AutoHideScrollBars;
            else
                return true;
        }
    }
    public static double TextScaleFactor
    {
        get => m_UISettings.TextScaleFactor;
    }
    #endregion

    #region [Machine info from Windows.Security.ExchangeActiveSyncProvisioning]
    public static string OperatingSystem
    {
        get => m_deviceInfo.OperatingSystem;
    }
    public static string DeviceManufacturer
    {
        get => m_deviceInfo.SystemManufacturer;
    }
    public static string DeviceModel
    {
        get => m_deviceInfo.SystemProductName;
    }
    public static string MachineName
    {
        get => m_deviceInfo.FriendlyName;
    }
    #endregion

    // https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/#advantages-and-disadvantages-of-packaging-your-app
#if IS_UNPACKAGED // We're using a custom PropertyGroup Condition we defined in the csproj to help us with the decision.
    public static bool IsPackaged { get => false; }
#else
    public static bool IsPackaged { get => true; }
#endif
#endregion

    /// <summary>
    /// Entry point to application.
    /// </summary>
    public App()
    {
        Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{MethodBase.GetCurrentMethod()?.Name}");

        AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomainFirstChanceException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

        // Is there more than one of us?
        InstanceMutex = new Mutex(true, Assembly.GetExecutingAssembly().GetName().Name, out bool isNew);
        if (isNew)
            IsMainInstance = true;
        else
            InstanceMutex.Close();

        WinRT.ComWrappersSupport.InitializeComWrappers();

        #region[System Identification]
        SessionID = Guid.NewGuid().ToString().ToUpperInvariant();
        if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.System.Profile.SystemIdentification")
                    && Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.System.Profile.SystemIdentification", "GetSystemIdForPublisher"))
        {
            // The system identification string is created using the calling app's publisher ID and an identifier
            // derived from the system's firmware/hardware. The system identification string will be identical for
            // all unpackaged (Win32) apps and all packaged apps with no publisher ID.
            var systemId = Windows.System.Profile.SystemIdentification.GetSystemIdForPublisher();

            if (systemId != null)
            {
                var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(systemId.Id);
                byte[] bytes = new byte[systemId.Id.Length];
                dataReader.ReadBytes(bytes);
                // The machineId could be used for software validation purposes through an API call.
                MachineID = BitConverter.ToString(bytes);
            }
            else
            {
                MachineID = $"No identification info was available.";
            }

        }
        if (IsPackaged)
        {
            try
            {   // This has not been tested; typical version query for a packaged UWP app...
                PackageVersion packageVersion = Package.Current.Id.Version;
                var versionString = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
            }
            catch (Exception) { }
        }
        #endregion

        GatherEnvironment();

        this.InitializeComponent();
    }

    /// <summary>
    /// Our core setup point, invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Determining passed arguments
        string[] argsAlt = Environment.GetCommandLineArgs();

        // This is currently a bug. https://github.com/microsoft/microsoft-ui-xaml/issues/3368
        if (args.Arguments.Length > 0)
        {
            if (args.Arguments.Contains("-"))
                args.Arguments.Split("-", StringSplitOptions.RemoveEmptyEntries).PopulateArgDictionary(ref ArgDictionary);
            else if (args.Arguments.Contains("/"))
                args.Arguments.Split("/", StringSplitOptions.RemoveEmptyEntries).PopulateArgDictionary(ref ArgDictionary);
            else
                args.Arguments.Split(" ", StringSplitOptions.RemoveEmptyEntries).PopulateArgDictionary(ref ArgDictionary);

            // Call secondary constructor.
            m_window = new MainWindow(ArgDictionary);
        }
        else if (argsAlt.Length > 0)
        {
            var array = argsAlt.IgnoreFirstTakeRest();
            for (int i = 0; i < array.Length; i += 2)
            {
                try
                {
                    ArgDictionary[array[i]] = array[i + 1];
                }
                catch (Exception ex) // probably out of bounds or key duplicate
                {
                    Debug.WriteLine($"Argument parsing: {ex.Message}", $"{nameof(App)}");
                }
            }

            // Call secondary constructor.
            m_window = new MainWindow(ArgDictionary);
        }
        else
        {
            // Call primary constructor.
            m_window = new MainWindow();
        }

        m_window.Activate();
        MainRoot = m_window.Content as FrameworkElement;
        var aw = GetAppWindow(m_window);
        if (aw != null)
        {
            // With the AppWindow we can configure other events:
            aw.Closing += OnWindowClosingEvent;

            SetIcon("Assets/RepoFolder.ico", aw);
            aw.Resize(new Windows.Graphics.SizeInt32(1000, 700));
            //aw.Move(new Windows.Graphics.PointInt32(200, 200));
            //aw.MoveAndResize(new Windows.Graphics.RectInt32(200, 200, 1100, 600));
        }
        CenterWindow(m_window);

        #region [Demo reading from local storage, if packaged app]
        if (App.IsPackaged)
        {
            // The SettingsHelper can be used from any module.
            if (App.SettingsHelper.KeyExists("StartTime"))
                Debug.WriteLine($"StartTime key from local storage => {App.SettingsHelper.Read<string>("StartTime")}");
            else
                App.SettingsHelper.Save<string>("StartTime", $"{DateTime.Now.ToJsonFriendlyFormat()}");
        }
        #endregion
    }

    #region [Helper methods]
    /// <summary>
    /// This code example demonstrates how to retrieve an AppWindow from a WinUI3 window.
    /// The AppWindow class is available for any top-level HWND in your app.
    /// AppWindow is available only to desktop apps (both packaged and unpackaged), it's not available to UWP apps.
    /// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/windowing/windowing-overview
    /// </summary>
    public Microsoft.UI.Windowing.AppWindow? GetAppWindow(object window)
    {
        // Retrieve the window handle (HWND) of the current (XAML) WinUI3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // For other classes to use.
        WindowHandle = hWnd;

        // Retrieve the WindowId that corresponds to hWnd.
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

        // Lastly, retrieve the AppWindow for the current (XAML) WinUI3 window.
        Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (appWindow != null)
        {
            // You now have an AppWindow object, and you can call its methods to manipulate the window.
            // As an example, let's change the title text of the window: appWindow.Title = "Title text updated via AppWindow!";
            //appWindow.Move(new Windows.Graphics.PointInt32(200, 100));
            appWindow?.MoveAndResize(new Windows.Graphics.RectInt32(250, 100, 1300, 800), Microsoft.UI.Windowing.DisplayArea.Primary);
        }

        return appWindow;
    }

    /// <summary>
    /// Use <see cref="Microsoft.UI.Windowing.AppWindow"/> to set the taskbar icon for WinUI application.
    /// This method has been tested with an unpackaged and packaged app.
    /// Setting the icon in the project file using the ApplicationIcon tag.
    /// </summary>
    /// <param name="iconName">the local path, including any subfolder, e.g. "Assets\Icon.ico"</param>
    /// <param name="appWindow"><see cref="Microsoft.UI.Windowing.AppWindow"/></param>
    void SetIcon(string iconName, Microsoft.UI.Windowing.AppWindow appWindow)
    {
        if (appWindow == null)
            return;

        try
        {
            if (IsPackaged)
                appWindow.SetIcon(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, iconName));
            else
                appWindow.SetIcon(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), iconName));
        }
        catch (Exception ex) { Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}"); }
    }

    /// <summary>
    /// Centers a <see cref="Microsoft.UI.Xaml.Window"/> based on the <see cref="Microsoft.UI.Windowing.DisplayArea"/>.
    /// </summary>
    void CenterWindow(Window window)
    {
        try
        {
            System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            if (Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId) is Microsoft.UI.Windowing.AppWindow appWindow &&
                Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest) is Microsoft.UI.Windowing.DisplayArea displayArea)
            {
                Windows.Graphics.PointInt32 CenteredPosition = appWindow.Position;
                CenteredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                CenteredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(CenteredPosition);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}"); }
    }

	/// <summary>
	/// The <see cref="DisplayArea"/> exposes properties such as:
	/// OuterBounds     (Rect32)
	/// WorkArea.Width  (int)
	/// WorkArea.Height (int)
	/// IsPrimary       (bool)
	/// DisplayId.Value (ulong)
	/// </summary>
	/// <param name="window"></param>
	/// <returns><see cref="DisplayArea"/></returns>
	DisplayArea? GetDisplayArea(Window window)
	{
		System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
		Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
		var da = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
		return da;
	}

	/// <summary>
	/// Returns the AssemblyVersion, not the FileVersion.
	/// </summary>
	public static Version GetCurrentAssemblyVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
    }

    /// <summary>
    /// Test method for side-loading of library objects using the <see cref="Assembly.GetExportedTypes()"/>.
    /// Most base types, such as <see cref="ZoomMode"/>, will expose an <see cref="IComparable"/>, <see cref="IConvertible"/> and <see cref="IFormattable"/>.
    /// </summary>
    /// <param name="dllPath"></param>
    public static Dictionary<string, Microsoft.UI.Composition.IAnimationObject> GetExportedInterfaces(string dllPath = "")
    {
        Dictionary<string, Microsoft.UI.Composition.IAnimationObject> evaluators = new();
        
        if (App.IsPackaged)
            return evaluators;

        if (string.IsNullOrEmpty(dllPath))
            dllPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Microsoft.WinUI.dll");

        try
        {
            Assembly dll = Assembly.LoadFile(dllPath);
            if (dll == null)
                return evaluators;

            foreach (Type t in dll.GetExportedTypes())
            {
                //var i = t.FindInterfaces(((typeIN, o) => typeIN.Name != null), null);
                //foreach (var n in i) { Debug.WriteLine($"[{t.Name}] => {n.Name}"); }

                if (t.FindInterfaces(((typeIN, o) => typeIN.Name == "IAnimationObject" && !t.ContainsGenericParameters), null).Length > 0)
                {
                    try
                    {
                        var obj = (Microsoft.UI.Composition.IAnimationObject)dll.CreateInstance(t.FullName);
                        if (obj != null) { evaluators.Add(t.Name, obj); }
                    }
                    catch (MissingMethodException ex)
                    {
                        Debug.WriteLine($"[{t.Name}] {ex.Message}", $"{nameof(App)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CreateInstance] {ex.Message}", $"{nameof(App)}");
                    }
                }
            }
            #region [All classes in Microsoft.WinUI.dll supporting Microsoft.UI.Composition.IAnimationObject]
            /*
            Ellipse
            Line
            Path
            Polygon
            Polyline
            Rectangle
            AcrylicBrush
            ImageBrush
            LinearGradientBrush
            RadialGradientBrush
            SolidColorBrush
            Glyphs
            AnimatedIcon
            AnimatedVisualPlayer
            AppBar
            AppBarButton
            AppBarElementContainer
            AppBarSeparator
            AppBarToggleButton
            AutoSuggestBox
            BitmapIcon
            Border
            BreadcrumbBar
            BreadcrumbBarItem
            Button
            CalendarDatePicker
            CalendarView
            CalendarViewDayItem
            Canvas
            CheckBox
            ColorPicker
            ComboBox
            ComboBoxItem
            CommandBar
            CommandBarOverflowPresenter
            ContentControl
            ContentDialog
            ContentPresenter
            DatePicker
            DropDownButton
            Expander
            FlipView
            FlipViewItem
            FlyoutPresenter
            FontIcon
            Frame
            Grid
            GridView
            GridViewHeaderItem
            GridViewItem
            GroupItem
            Hub
            HubSection
            HyperlinkButton
            IconSourceElement
            Image
            ImageIcon
            InfoBadge
            InfoBar
            ItemsControl
            ItemsPresenter
            ItemsRepeater
            ItemsRepeaterScrollHost
            ItemsStackPanel
            ItemsWrapGrid
            ListBox
            ListBoxItem
            ListView
            ListViewHeaderItem
            ListViewItem
            MediaPlayerElement
            MediaPlayerPresenter
            MediaTransportControls
            MenuBar
            MenuBarItem
            MenuFlyoutItem
            MenuFlyoutPresenter
            MenuFlyoutSeparator
            MenuFlyoutSubItem
            NavigationView
            NavigationViewItem
            NavigationViewItemHeader
            NavigationViewItemSeparator
            NumberBox
            Page
            ParallaxView
            PasswordBox
            PathIcon
            PersonPicture
            PipsPager
            Pivot
            PivotItem
            ProgressBar
            ProgressRing
            RadioButton
            RadioButtons
            RadioMenuFlyoutItem
            RatingControl
            RefreshContainer
            RefreshVisualizer
            RelativePanel
            RevealListViewItemPresenter
            RichEditBox
            RichTextBlock
            RichTextBlockOverflow
            ScrollContentPresenter
            ScrollViewer
            SemanticZoom
            Slider
            SplitButton
            SplitView
            StackPanel
            SwapChainBackgroundPanel
            SwapChainPanel
            SwipeControl
            SymbolIcon
            TabView
            TabViewItem
            TeachingTip
            TextBlock
            TextBox
            TimePicker
            ToggleMenuFlyoutItem
            ToggleSplitButton
            ToggleSwitch
            ToolTip
            TreeView
            TreeViewItem
            TreeViewList
            TwoPaneView
            UserControl
            VariableSizedWrapGrid
            Viewbox
            VirtualizingStackPanel
            WebView2
            WrapGrid
            CalendarPanel
            CarouselPanel
            ColorPickerSlider
            ColorSpectrum
            CommandBarFlyoutCommandBar
            GridViewItemPresenter
            InfoBarPanel
            ListViewItemPresenter
            MonochromaticOverlayPresenter
            NavigationViewItemPresenter
            PivotHeaderPanel
            PivotPanel
            Popup
            RepeatButton
            ScrollBar
            TabViewListView
            Thumb
            TickBar
            ToggleButton
            */
            #endregion
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetExportedInterfaces: {ex.Message}", $"{nameof(App)}");
        }

        return evaluators;
    }

    /// <summary>
    /// Returns the declaring type's namespace.
    /// </summary>
    public static string? GetCurrentNamespace()
    {
        return System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace;
    }

    /// <summary>
    /// Returns the declaring type's assembly name.
    /// </summary>
    public static string? GetCurrentAssemblyName()
    {
        return System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly.FullName;
    }

    /// <summary>
    /// Returns the amount of CPU time this process has used.
    /// </summary>
    public static string GetCPUTime()
    {
        try
        {
            var process = Windows.System.Diagnostics.ProcessDiagnosticInfo.GetForCurrentProcess();
            var report = process.CpuUsage.GetReport();
            return $"{report.UserTime.ToReadableTime()}";

            #region [iterating all processes]
            // Get a list of running apps (filtered by permission level).
            IAsyncOperation<IList<AppDiagnosticInfo>> infoOperation = AppDiagnosticInfo.RequestInfoAsync();
            Task<IList<AppDiagnosticInfo>> infoTask = infoOperation.AsTask();
            infoTask.Wait(); // We wait until the task is finished and then access the result.
            IList<AppDiagnosticInfo> runningApps = infoTask.Result;

            foreach (AppDiagnosticInfo app in runningApps)
            {
                // Get the data from group and process level.
                IList<AppResourceGroupInfo> groups = app.GetResourceGroups();
                if (groups != null)
                {
                    foreach (AppResourceGroupInfo group in groups)
                    {
                        // Get the total usage of processes for this app.
                        ulong totalProcessPrivateCommit = 0;
                        TimeSpan totalProcessTime = TimeSpan.Zero;
                        IList<ProcessDiagnosticInfo> processes = group.GetProcessDiagnosticInfos();
                        if (processes != null)
                        {
                            foreach (ProcessDiagnosticInfo proc in processes)
                            {
                                if (proc.IsPackaged)
                                {
                                    Microsoft.UI.Xaml.Media.Imaging.BitmapImage? logo = GetPackagedLogoAsync(app).GetAwaiter().GetResult();
                                }

                                totalProcessPrivateCommit += GetProcessPrivateCommit(proc);
                                totalProcessTime += GetProcessCPUTime(proc);

                                var PID = proc.ProcessId;
                                var EFN = proc.ExecutableFileName;
                                var PST = proc.ProcessStartTime;

                                #region [other reports]
                                TimeSpan kernel = TimeSpan.Zero;
                                TimeSpan user = TimeSpan.Zero;

                                ulong npp = 0; ulong pp = 0; ulong pFault = 0; ulong pFile = 0;
                                ulong pNpp = 0; ulong pPP = 0; ulong ppFile = 0; ulong pVirt = 0;
                                ulong pWSet = 0; ulong ppc = 0; ulong vm = 0; ulong ws = 0; 
                                long br = 0; long bw = 0; long ob = 0;
                                long oo = 0; long ro = 0; long wo = 0;

                                // Processor Set
                                ProcessCpuUsageReport pcReport = proc.CpuUsage.GetReport();
                                if (pcReport != null)
                                {
                                    kernel = pcReport.KernelTime;
                                    user = pcReport.UserTime;
                                }

                                // Memory Set
                                ProcessMemoryUsageReport pmReport = proc.MemoryUsage.GetReport();
                                if (pmReport != null)
                                {
                                    npp = pmReport.NonPagedPoolSizeInBytes;
                                    pp = pmReport.PagedPoolSizeInBytes;
                                    pFault = pmReport.PageFaultCount;
                                    pFile = pmReport.PageFileSizeInBytes;
                                    pNpp = pmReport.PeakNonPagedPoolSizeInBytes;
                                    pPP = pmReport.PeakPagedPoolSizeInBytes;
                                    ppFile = pmReport.PeakPageFileSizeInBytes;
                                    pVirt = pmReport.PeakVirtualMemorySizeInBytes;
                                    pWSet = pmReport.PeakWorkingSetSizeInBytes;
                                    ppc = pmReport.PrivatePageCount;
                                    vm = pmReport.VirtualMemorySizeInBytes;
                                    ws = pmReport.WorkingSetSizeInBytes;
                                }

                                // Disk Set
                                ProcessDiskUsageReport pdReport = proc.DiskUsage.GetReport();
                                if (pdReport != null)
                                {
                                    br = pdReport.BytesReadCount;
                                    bw = pdReport.BytesWrittenCount;
                                    ob = pdReport.OtherBytesCount;
                                    oo = pdReport.OtherOperationCount;
                                    ro = pdReport.ReadOperationCount;
                                    wo = pdReport.WriteOperationCount;
                                }
                                #endregion
                            }
                        }
                    }
                }
            }
            #endregion
        }
        catch (Exception) { return "N/A"; }
    }

    /// <summary>
    /// Returns the amount of PrivateBytes this process is using.
    /// </summary>
    /// <remarks>
    /// WorkingSet is the amount of memory the OS has allocated to the process.
    /// The working set contains only pageable memory allocations; nonpageable memory allocations such as 
    /// Address Windowing Extensions (AWE) or large page allocations are not included in the working set.
    /// When a process references pageable memory that is not currently in its working set, a page fault 
    /// occurs. The system page fault handler attempts to resolve the page fault and, if it succeeds, 
    /// the page is added to the working set. Each process has a minimum and maximum working set size 
    /// that affect the virtual memory paging behavior of the process.
    /// </remarks>
    public static string GetMemoryCommit()
    {
        try
        {
            var process = Windows.System.Diagnostics.ProcessDiagnosticInfo.GetForCurrentProcess();
            var report = process.MemoryUsage.GetReport();
            return $"{report.PrivatePageCount.ToFileSize()}";
        }
        catch (Exception) { return "N/A"; }
    }

    static ulong GetProcessPrivateCommit(ProcessDiagnosticInfo process)
    {
        ulong privateCommit = 0;
        if (process.MemoryUsage != null)
        {
            ProcessMemoryUsageReport pmReport = process.MemoryUsage.GetReport();
            if (pmReport != null)
            {
                privateCommit = pmReport.PageFileSizeInBytes;
            }
        }
        return privateCommit;
    }

    static TimeSpan GetProcessCPUTime(ProcessDiagnosticInfo process)
    {
        TimeSpan privateTime = TimeSpan.Zero;
        if (process.MemoryUsage != null)
        {
            ProcessCpuUsageReport pcReport = process.CpuUsage.GetReport();
            if (pcReport != null)
            {
                privateTime = pcReport.UserTime;
            }
        }
        return privateTime;
    }

    static async Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage?> GetPackagedLogoAsync(Windows.System.AppDiagnosticInfo app)
    {
        try
        {
            Windows.Storage.Streams.RandomAccessStreamReference stream = app.AppInfo.DisplayInfo.GetLogo(new Size(64, 64));
            Windows.Storage.Streams.IRandomAccessStreamWithContentType content = await stream.OpenReadAsync();
            Microsoft.UI.Xaml.Media.Imaging.BitmapImage bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            await bitmapImage.SetSourceAsync(content);
            return bitmapImage;
        }
        catch (Exception) { return null; }
    }

    static Windows.System.Display.DisplayRequest? appDisplayRequest;
    static bool isDisplayRequestActive = false;
    /// <summary>
    /// Keep the screen alive as long as the app is running.
    /// This is equivalent to SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS).
    /// </summary>
    public static void ActivateDisplayRequest()
    {
        if (!isDisplayRequestActive)
        {
            try
            {
                if (appDisplayRequest == null)
                {
                    appDisplayRequest = new Windows.System.Display.DisplayRequest();
                }
                appDisplayRequest.RequestActive();
                isDisplayRequestActive = true;
            }
            catch (Exception) { }
        }
    }
    #endregion

    #region [Domain Events]
    void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        Debug.WriteLine($"[OnProcessExit] {sender?.GetType()}");
    }

    void CurrentDomainFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        Debug.WriteLine($"First chance exception: {e.Exception}");
    }

    void CurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Exception? ex = e.ExceptionObject as Exception;
        Debug.WriteLine($"Thread exception of type {ex?.GetType()}: {ex}");
    }

    /// <summary>
    /// Occurs when an exception is not handled on a background thread.
    /// i.e. A task is fired and forgotten Task.Run(() => {...})
    /// </summary>
    void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($">> OnUnobservedTaskException: {e.Exception}");
        e.SetObserved(); // suppress and handle manually
    }

    /// <summary>
    /// How to load custom libraries based on some environment factor.
    /// In this example we pretend to load the correct bit-version
    /// of a 3rd party dynamic link library.
    /// </summary>
    /// <returns>The resolved <see cref="Assembly"/>.</returns>
    Assembly? CurrentDomainOnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        Debug.WriteLine($"Inside AssemblyResolve with {args.Name}");
        //Inside AssemblyResolve with System.Private.CoreLib.XmlSerializers, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e, processorArchitecture=AMD64

        if (args.Name.StartsWith("SQLite"))
        {
            string assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
            string arcSpecificPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? Directory.GetCurrentDirectory(),
                Environment.Is64BitProcess ? "x64" : "x86",
                assemblyName);

            return File.Exists(arcSpecificPath) ? Assembly.LoadFile(arcSpecificPath) : null;
        }

        return null;
    }

    void CurrentDomainOnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        Debug.WriteLine($"{args.LoadedAssembly.FullName} is being loaded.");
        try
        {
            // Prevent exceptions from run-time generated assemblies that do not have a location property.
            if (!string.IsNullOrEmpty(args.LoadedAssembly.FullName) &&
                !args.LoadedAssembly.FullName.Contains("Snippets") &&
                !args.LoadedAssembly.FullName.Contains("Microsoft.GeneratedCode"))
                Debug.WriteLine($"Location={args.LoadedAssembly.Location}");
        }
        catch (NotSupportedException)
        {
            Debug.WriteLine($"Name={args.LoadedAssembly.FullName}: UNKNOWN LOCATION");
        }
    }

    void OnWindowClosingEvent(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        IsClosing = true;
        Debug.WriteLine($"[OnWindowClosing] Cancel={args.Cancel}");
        OnWindowClosing?.Invoke(DateTime.Now.ToString("hh:mm:ss.fff tt"));
    }
    #endregion

    #region [Home-brew service getter]
    /// <summary>
    /// Returns an instance of the desired service type. Use this if you can't/won't pass
    /// the service through the target's constructor. Can be used from any window/page/model.
    /// The 1st call to this method will add and instantiate the pre-defined services.
    /// </summary>
    /// <example>
    /// var service1 = App.GetService{SomeViewModel}();
    /// var service2 = App.GetService{FileLogger}();
    /// </example>
    public static T? GetService<T>() where T : class
    {
        try
        {
            // New-up the services container if needed.
            if (ServicesHost == null) { ServicesHost = new List<Object>(); }

            // If 1st time then add relevant services to the container.
            // This could be done elsewhere, e.g. in the main constructor.
            if (ServicesHost.Count == 0)
            {
                ServicesHost?.Add(new MainViewModel());
                ServicesHost?.Add(new FileLogger(System.IO.Path.Combine(FileLogger.GetRoot(), "Logs"), LogLevel.Debug));
            }

            // Try and locate the desired service. We're not using FirstOrDefault
            // here so that a null will be returned when an exception is thrown.
            var vm = ServicesHost?.Where(o => o.GetType() == typeof(T)).First();

            if (vm != null)
                return (T)vm;
            else
                throw new ArgumentException($"{typeof(T)} must be registered first within {MethodBase.GetCurrentMethod()?.Name}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
            return null;
        }
    }
    public static List<Object>? ServicesHost { get; private set; }
    #endregion

    #region [Environment extras]
    /// <summary>
    /// The <see cref="Windows.ApplicationModel.Package"/> object will be null when running as an unpackaged application.
    /// </summary>
    void SampleWindowsApplicationModel()
    {
        if (!App.IsPackaged) { return; }

        string? pdn = Windows.ApplicationModel.Package.Current.DisplayName;
        Windows.ApplicationModel.PackageVersion pv = Windows.ApplicationModel.Package.Current.Id.Version;
        Windows.System.ProcessorArchitecture ppa = Windows.ApplicationModel.Package.Current.Id.Architecture;
    }

    void GatherEnvironment()
    {
        // Get the environment variables.
        IDictionary procVars = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.Process);

        // Adding names and variables that exist.
        foreach (DictionaryEntry pVar in procVars)
        {
            string? pVarKey = (string?)pVar.Key;
            string? pVarValue = (string?)pVar.Value ?? "";
            if (!string.IsNullOrEmpty(pVarKey) && !MachineAndUserVars.ContainsKey(pVarKey))
            {
                MachineAndUserVars.Add(pVarKey, pVarValue);
            }
        }
    }

    /// <summary>
    /// Returns the variables for the specified target. Errors that occurs will be caught and logged.
    /// </summary>
    /// <param name="target">The target variable source of the type <see cref="EnvironmentVariableTarget"/> </param>
    /// <returns>A dictionary with the variable or an empty dictionary on errors.</returns>
    IDictionary GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget target)
    {
        try
        {
            return Environment.GetEnvironmentVariables(target);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception while getting the environment variables for target '{target}': {ex.Message}");
            return new Hashtable(); // HashTable inherits from IDictionary
        }
    }
    #endregion
}
