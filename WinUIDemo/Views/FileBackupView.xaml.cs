#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Xml.Serialization;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.System.Threading;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using Windows.UI.Text;
using Windows.UI.ViewManagement;

using Path = System.IO.Path;

using WinUIDemo.Models;
using WinUIDemo.Support;
using WinUIDemo.Controls;
using Windows.Graphics.Printing;
using WinUIDemo.Printing;
using Microsoft.UI.Xaml.Documents;

namespace WinUIDemo.Views;

/// <summary>
/// This class contains more than is needed for a UserControl,
/// but some of these methods are for testing WinUI feature sets.
/// https://learn.microsoft.com/en-us/windows/apps/design/layout/alignment-margin-padding
/// https://learn.microsoft.com/en-us/windows/apps/design/layout/layouts-with-xaml
/// https://learn.microsoft.com/en-us/windows/apps/winui/winui3/xaml-templated-controls-csharp-winui-3
/// </summary>
public sealed partial class FileBackupView : UserControl
{
    #region [Properties]
    // Shimmer effect props: We'll need to define these up top so the GC doesn't get them later.
    static Microsoft.UI.Composition.Compositor? _compositor;
    static Microsoft.UI.Composition.Visual? _textVisual;
    static Microsoft.UI.Composition.PointLight? _pointLight;
    static Microsoft.UI.Composition.PointLight? _pointLight2;

	static bool _tipShown = false;
    static bool _evaluating = false;
    static bool _startupFinished = false;
    static string _lastError = "";
    static readonly object _obj = new object();
    DeviceWatcherHelper? _dwh;
    ThreadPoolTimer? _periodicTimer;
    DispatcherTimer? _autoCloseTimer;
    DispatcherQueueTimer? _debounceTimer;
    ValueStopwatch _elapsed = new ValueStopwatch();
    static CancellationTokenSource? _cts;
    readonly static SlimLock _lock = SlimLock.Create();
    static Queue<LogEntry> _logQueue = new Queue<LogEntry>();
    public ViewModels.MainViewModel? ViewModel { get; private set; }
    public FileLogger? Logger { get; private set; }
    public SettingsManager AppSettings { get; private set; }
    //public Settings Config { get; private set; } = new();
    public List<string> AutoSuggestions { get; private set; } = new();
    public List<MRU> MostRecent { get; private set; } = new();
    public DateTime PageTime { get; private set; } = DateTime.Now;
    public List<int> ThreadCount { get; } = new() { 2, 3, 4, 5, 6, 7, 8 };
    public List<int> StaleCount { get; } = new() { 2, 4, 6, 8, 10, 15, 30, 60, 90, 365 };

    private ObservableCollection<LogEntry> _logMessages = new();
    public ObservableCollection<LogEntry> LogMessages
    {
        get { return _logMessages; }
        set { _logMessages = value; }
    }

    private ObservableCollection<string> _messages = new();
    public ObservableCollection<string> Messages
    {
        get { return _messages; }
        set { _messages = value; }
    }

    private string _repoPath = "";
    public string RepoPath
    {
        get { return _repoPath; }
        set { _repoPath = value; }
    }
    public bool IsBusy
    {
        get { return (bool)GetValue(IsBusyProperty); }
        set 
        {
            if (App.IsClosing) { return; }
            SetValue(IsBusyProperty, value); 
        }
    }
    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy),
        typeof(bool),
        typeof(FileBackupView),
        new PropertyMetadata(false));

    public string Progress
    {
        get { return (string)GetValue(ProgressProperty); }
        set 
        {
            if (App.IsClosing) { return; }
            // SetValue is not thread-safe when running from a timer or sub-task.
            if (DispatcherQueue.HasThreadAccess)
                SetValue(ProgressProperty, value);
            else
                DispatcherQueue.TryEnqueue(() => { SetValue(ProgressProperty, value); });
        }
    }
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(string),
        typeof(FileBackupView),
        new PropertyMetadata(""));

    public string TotalCPU
    {
        get { return (string)GetValue(TotalCPUProperty); }
        set
        {
            if (App.IsClosing) { return; }
            // SetValue is not thread-safe when running from a timer or sub-task.
            if (DispatcherQueue.HasThreadAccess)
                SetValue(TotalCPUProperty, value);
            else
                DispatcherQueue.TryEnqueue(() => { SetValue(TotalCPUProperty, value); });
        }
    }
    public static readonly DependencyProperty TotalCPUProperty = DependencyProperty.Register(
        nameof(TotalCPU),
        typeof(string),
        typeof(FileBackupView),
        new PropertyMetadata(""));

    public string MemoryCount
    {
        get { return (string)GetValue(MemoryCountProperty); }
        set
        {
            if (App.IsClosing) { return; }
            // SetValue is not thread-safe when running from a timer or sub-task.
            if (DispatcherQueue.HasThreadAccess)
                SetValue(MemoryCountProperty, value);
            else
                DispatcherQueue.TryEnqueue(() => { SetValue(MemoryCountProperty, value); });
        }
    }
    public static readonly DependencyProperty MemoryCountProperty = DependencyProperty.Register(
        nameof(MemoryCount),
        typeof(string),
        typeof(FileBackupView),
        new PropertyMetadata(""));

    public string InspectionCount
    {
        get { return (string)GetValue(InspectionCountProperty); }
        set
        {
            if (App.IsClosing) { return; }
            // SetValue is not thread-safe when running from a timer or sub-task.
            if (DispatcherQueue.HasThreadAccess)
                SetValue(InspectionCountProperty, value);
            else
                DispatcherQueue.TryEnqueue(() => { SetValue(InspectionCountProperty, value); });
        }
    }
    public static readonly DependencyProperty InspectionCountProperty = DependencyProperty.Register(
        nameof(InspectionCount),
        typeof(string),
        typeof(FileBackupView),
        new PropertyMetadata(""));

    public string DiskSpace
    {
        get { return (string)GetValue(DiskSpaceProperty); }
        set
        {
            if (App.IsClosing) { return; }
            // SetValue is not thread-safe when running from a timer or sub-task.
            if (DispatcherQueue.HasThreadAccess)
                SetValue(DiskSpaceProperty, value);
            else
                DispatcherQueue.TryEnqueue(() => { SetValue(DiskSpaceProperty, value); });
        }
    }
    public static readonly DependencyProperty DiskSpaceProperty = DependencyProperty.Register(
        nameof(DiskSpace),
        typeof(string),
        typeof(FileBackupView),
        new PropertyMetadata(""));

    #endregion

    #region [Commands]
    public ICommand OpenLogFileCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand TestingCommand { get; }

    ICommandHandler<KeyRoutedEventArgs> _keyboardHandler;
    ICommandHandler<PointerRoutedEventArgs> _mouseHandler;
    #endregion

    /// <summary>
    /// Default Constructor
    /// </summary>
    public FileBackupView()
    {
        Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{MethodBase.GetCurrentMethod()?.Name}");

        this.InitializeComponent();

        // Demonstrate our service getter.
        ViewModel = App.GetService<ViewModels.MainViewModel>();

        #region [ILogger]
        Logger = App.GetService<FileLogger>();
        
        if (Logger != null)
            Logger.OnException += LoggerOnException;
        #endregion

        #region [ISettingsManager]
        AppSettings = App.GetService<SettingsManager>() ?? new SettingsManager();
        #endregion

        #region [Configure Commands]
        OpenLogFileCommand = new RelayCommand<object>((ctrl) => OpenLogFile(ctrl));
        OpenAboutCommand = new RelayCommand<object>(async (ctrl) => await OpenAbout(ctrl));
        TestingCommand = new RelayCommand<object>((o) => TestCommandParameter(o));
        InitializeKeyboardHandler();
        InitializeMouseHandler();
        this.KeyDown += FileBackupViewOnKeyDown;
        RootGrid.KeyDown += FileBackupViewOnKeyDown;
        RootGrid.PointerWheelChanged += FileBackupViewOnPointerWheelChanged;
        #endregion

        #region [Register Events]
        this.Loaded += FileBackupViewOnLoaded;
        this.SizeChanged += FileBackupViewOnSizeChanged;
        App.OnWindowClosing += AppOnWindowClosing;
        SettingsSplitView.PaneOpened += OnPaneOpenedOrClosed;
        SettingsSplitView.PaneClosed += OnPaneOpenedOrClosed;
        //SettingsSplitView.PointerExited += (_,_) => { SettingsSplitView.IsPaneOpen = false; }; // You may or may not want this sensitivity to auto-close.
        teachingTip.RegisterPropertyChangedCallback(TeachingTip.IsOpenProperty, IsTipOpenChanged);
        tbPath.RegisterPropertyChangedCallback(TextBox.FocusStateProperty, IsFocusStateChanged);
        InputStoryboardHide.Completed += InputStoryboardHideOnCompleted;
        InputStoryboardShow.Completed += InputStoryboardShowOnCompleted;
        #endregion

        #region [Collection Changed Routines]
        Messages.CollectionChanged += MessagesOnCollectionChanged;

        // https://github.com/mikoskinen/blog
        // We need to know if the ListView is scrolled to the bottom. In order to achieve these, we first
        // need to access the ListView's ScrollViewer and then the ScrollViewer's vertical ScrollBar. We can
        // use the GetFirstDescendantOfType and GetDescendantsOfType -extension methods from WinRT XAML Toolkit: 
        /*
        ScrollViewer scrollViewer = lvMessages.GetFirstDescendantOfType<ScrollViewer>();
        List<ScrollBar> scrollbars = scrollViewer.GetDescendantsOfType<ScrollBar>().ToList();
        ScrollBar verticalBar = scrollbars.FirstOrDefault(x => x.Orientation == Orientation.Vertical);

        if (verticalBar != null)
        {   // Now that we have the vertical scroll bar, we can register an event handler to its Scroll-event:
            verticalBar.Scroll += VerticalBarScroll;
        }
        */
        #endregion

        _debounceTimer = DispatcherQueue.CreateTimer();

        // The idea behind this logger is to only log errors/issues for the user to review so that the file
        // size will not become unwieldy, but we'll at least log the init so that there's something to look at.
        _logQueue.Enqueue(new LogEntry { Message = $"Deferred log queue test entry", Method = "FileBackupView", Severity = LogLevel.Debug, Time = DateTime.Now });
    }

    #region [Keyboard Handler]
    void FileBackupViewOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var result = _keyboardHandler.Handle(e);
        if (result.ShouldHandle) { e.Handled = true; }
    }

    void InitializeKeyboardHandler()
    {
        _keyboardHandler = new KeyboardCommandHandler(new List<IKeyboardCommand<KeyRoutedEventArgs>>()
        {
            new KeyboardCommand<KeyRoutedEventArgs>(Windows.System.VirtualKey.Escape, (args) =>
            {
                AppOnWindowClosing("Escape");
                Application.Current.Exit();
            }),
            new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, Windows.System.VirtualKey.B, (args) =>
            {
                btnBackupOnClick(this, new RoutedEventArgs());
            }),
            new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, Windows.System.VirtualKey.A, async (args) =>
            {
                await OpenAbout(this);
            }),
            new KeyboardCommand<KeyRoutedEventArgs>(Windows.System.VirtualKey.F1, (args) => 
            { 
                OnSettingsClick(this, new RoutedEventArgs()); 
            }),
            new KeyboardCommand<KeyRoutedEventArgs>(Windows.System.VirtualKey.F3, (args) =>
            {
                if (infoBar.IsOpen)
                {
                    NotifyInfoBar("Close me first", InfoBarSeverity.Warning);
                }
                else if (spInputControl.Visibility == Visibility.Visible)
                {
                    InputStoryboardHide.Begin();
                    //spInputControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    InputStoryboardShow.Begin();
                    spInputControl.Visibility = Visibility.Visible;
                }
            })
        });
    }

    /// <summary>
    /// We using the KeyboardCommandHandler now instead of the Grid.KeyboardAccelerators
    /// </summary>
    /// <param name="sender"><see cref="KeyboardAccelerator"/></param>
    /// <param name="args"><see cref="KeyboardAcceleratorInvokedEventArgs"/></param>
    void KeyboardAcceleratorF1Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_debounceTimer == null)
            _debounceTimer = DispatcherQueue.CreateTimer();

        // The key event can fire twice on closing, so we'll only execute this
        // code after some amount of time has elapsed since the last trigger.
        _debounceTimer?.Debounce(async () =>
        {
            Debug.WriteLine($"[Debounce F1Invoked {DateTime.Now.ToString("hh:mm:ss.fff tt")}]");
            if (SettingsSplitView.IsPaneOpen)
                SettingsSplitView.IsPaneOpen = false;
            else
                SettingsSplitView.IsPaneOpen = true;

        }, TimeSpan.FromSeconds(0.5));
    }
    #endregion

    #region [Mouse Handler]
    void InitializeMouseHandler()
    {
        _mouseHandler = new MouseCommandHandler(new List<IMouseCommand<PointerRoutedEventArgs>>()
        {
            new MouseCommand<PointerRoutedEventArgs>(true, false, false, false, false, false, (args) => 
            {
                ChangeFontOnMouseInput(args);
            }),
        }, this);
    }

    void FileBackupViewOnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var result = _mouseHandler.Handle(e);

        // Always handle it so ScrollViewer won't pick up the event.
        e.Handled = true;
    }

    /// <summary>
    /// Ctrl+Wheel changes font font size for logging messages.
    /// </summary>
    void ChangeFontOnMouseInput(PointerRoutedEventArgs args)
    {
        if (!_startupFinished) { return; }

        var mouseWheelDelta = args.GetCurrentPoint(this).Properties.MouseWheelDelta;

        if (mouseWheelDelta > 0) // positive delta = UP
            Write($"Increase font size here ({mouseWheelDelta})", LogLevel.Debug);
        else if (mouseWheelDelta < 0) // negative delta = DOWN
            Write($"Decrease font size here ({mouseWheelDelta})", LogLevel.Debug);
    }
    #endregion

    #region [Events]
    void FileBackupViewOnLoaded(object sender, RoutedEventArgs e)
    {
        #region [Apply Settings]
        cbWorkLocation.IsChecked = AppSettings.Config.AtWork;
        cbOpenExplorer.IsChecked = AppSettings.Config.ExplorerShell;
        cbBackupInitial.IsChecked = AppSettings.Config.FullInitialBackup;
        cbRandomBackdrop.IsChecked = AppSettings.Config.RandomBackdrop;
        ThreadComboBox.SelectedIndex = AppSettings.Config.ThreadIndex;
        SpoiledComboBox.SelectedIndex = AppSettings.Config.StaleIndex;
        if (AppSettings.Config.AtWork)
            tbBuffer.Text = AppSettings.Config.WorkBufferFolder;
        else
            tbBuffer.Text = AppSettings.Config.HomeBufferFolder;

        // Test our JSON extension method via logging.
        var json = AppSettings.Config.ToJsonObject();
        Logger?.WriteLine($"{json}", LogLevel.Info);
        #endregion

        #region [Load Most Recently Used]
        LoadMRU();
        var latest = MostRecent.Select(o => o).OrderByDescending(m => m.Time);
        if (latest != null) 
        {
            int tally = 0;
            tbPath.Text = latest.FirstOrDefault()?.Folder;
            foreach (var mru in MostRecent)
            {
                tally += mru.Count;
                // Check MRU freshness...
                int days = StaleCount[AppSettings.Config.StaleIndex] * -1;
                if (mru.Time < DateTime.Now.AddDays(days))
                    Write($"'{mru.Folder}' needs to be evaluated soon.", LogLevel.Notice);
            }
            InspectionCount = "Total files inspected: " + tally.ToString("#,###,###,##0");
        }
        #endregion

        #region [Load auto-complete suggestions]
        //string lastEntry = @"C:\Users\UserName\source\repos";
        //AutoSuggestions.Clear();
        //var ta = Task.Run(() => 
        //{
        //    try
        //    {
        //        var hash = Extensions.ReadLines(@".\Assets\Suggestions.txt");
        //        foreach (string line in hash) 
        //        { 
        //            AutoSuggestions.Add(line);
        //            lastEntry = line;
        //        }
        //    }
        //    catch (Exception ex) 
        //    { 
        //        NotifyInfoBar(ex.Message, InfoBarSeverity.Error); 
        //    }
        //}).GetAwaiter();
        //ta.OnCompleted(() => 
        //{ 
        //    NotifyInfoBar($"Loaded {AutoSuggestions.Count()} paths", InfoBarSeverity.Success);
        //    if (DispatcherQueue.HasThreadAccess)
        //        tbPath.Text = lastEntry;
        //    else
        //        DispatcherQueue.TryEnqueue(() => { tbPath.Text = lastEntry; });
        //});
        #endregion

        #region [Usage and SysInfo]
        // Gather client machine info.
        Task.Run(delegate ()
        {
            UpdateSystemInfo();

        }).GetAwaiter().OnCompleted(() => { Debug.WriteLine($"Update system info call finished."); });

        // CPU use percentage monitor.
        Task.Run(delegate ()
        {
            CpuUsageCalculator.Run(App.GetCurrentAssemblyName()?.Split(',')[0], 3000, ref cpuUsage, ref cpuRunning);

        }).GetAwaiter().OnCompleted(() => { Debug.WriteLine($"CPU usage calculator call finished."); });

        try
        {
            var processor = App.MachineAndUserVars["PROCESSOR_IDENTIFIER"];
            if (!string.IsNullOrEmpty(processor))
                Write($"Using {App.MachineAndUserVars["PROCESSOR_IDENTIFIER"]} with {App.MachineAndUserVars["NUMBER_OF_PROCESSORS"]} processors", LogLevel.Info);
        }
        catch (KeyNotFoundException) { }
        
        if (App.IsPackaged)
            Write($"Available App Memory: {App.AvailableMemory.ToFileSize()}", LogLevel.Info);

        Write($"{App.MachineName} ({App.DeviceManufacturer} {App.DeviceModel})", LogLevel.Info);
        #endregion

        #region [Shimmer effect animation]
        if (App.AnimationsEffectsEnabled)
        {
            float width = 400;
            float height = 30;
            float depth = 24; // based on font size
                              // Get interop compositor
            _compositor = ElementCompositionPreview.GetElementVisual(tbAppName).Compositor;
            // Get interop visual for XAML TextBlock
            _textVisual = ElementCompositionPreview.GetElementVisual(tbAppName);
            if (_compositor != null && _textVisual != null)
            {
                // Foreground should be bright so the point light animation can be visible.
                tbAppName.Foreground = new SolidColorBrush(Colors.White);
                tbAppName.FontWeight = Microsoft.UI.Text.FontWeights.Bold;

                #region [1st PointLight]
                _pointLight = _compositor.CreatePointLight();
                _pointLight.Color = Microsoft.UI.Colors.CornflowerBlue;
                _pointLight.Intensity = 1.0f;
                _pointLight.LinearAttenuation = 0.0f;
                _pointLight.CoordinateSpace = _textVisual; //set up coordinate space for offset
                _pointLight.Targets.Add(_textVisual); //target XAML TextBlock

                // Start out to the left, vertically centered, and light's z-offset is related to FontSize.
                // The tb.ActualWidth and tb.ActualHeight will not be known yet, so just use the total width & height.
                _pointLight.Offset = new System.Numerics.Vector3(-width, height, depth);

                // Create an Offset.X animation that runs forever.
                var anim = _compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0.5f, 0.4f * width);
                anim.InsertKeyFrame(1.0f, 1f * height);
                anim.Duration = TimeSpan.FromSeconds(6.7f);
                //anim.IterationCount = int.MaxValue;
                anim.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
                // Start the X-axis animation 
                _pointLight.StartAnimation("Offset.X", anim);
                #endregion

                #region [2nd PointLight]
                _pointLight2 = _compositor.CreatePointLight();
                _pointLight2.Color = Microsoft.UI.Colors.DarkTurquoise;
                _pointLight2.Intensity = 1.0f;
                _pointLight2.LinearAttenuation = 0.0f;
                _pointLight2.CoordinateSpace = _textVisual; //set up coordinate space for offset
                _pointLight2.Targets.Add(_textVisual); //target XAML TextBlock

                // Start out to the left, vertically centered, and light's z-offset is related to FontSize.
                // The tb.ActualWidth and tb.ActualHeight will not be known yet, so just use the total width & height.
                _pointLight2.Offset = new System.Numerics.Vector3(-width, height, depth);

                // Create an Offset.X animation that runs forever.
                var anim2 = _compositor.CreateScalarKeyFrameAnimation();
                anim2.InsertKeyFrame(0.5f, 0.6f * width);
                anim2.InsertKeyFrame(1.0f, 1f * height);
                anim2.Duration = TimeSpan.FromSeconds(8.2f);
                //anim2.IterationCount = int.MaxValue;
                anim2.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
                // Start the X-axis animation 
                _pointLight2.StartAnimation("Offset.X", anim2);
                #endregion
            }
        }
        #endregion

        #region [Superfluous]
        Task.Run(async delegate () { await CheckJumpList(); });

        Task.Run(async delegate () 
        {
            string filePath = ""; string basePath = "";

            if (App.IsPackaged)
                basePath = ApplicationData.Current.LocalFolder.Path;
            else
                basePath = Directory.GetCurrentDirectory();

            filePath = Path.Combine(basePath, @"MRU.xml");

            if (File.Exists(filePath))
            {
                TextFile textFile = await PackagedFileSystemUtility.ReadFile(filePath, ignoreFileSizeLimit: true, Extensions.GetEncoding(filePath));
            }
        });

        // Prevent idle-timeout while app is running.
        App.ActivateDisplayRequest();

        //LogAssemblies();
        //LogPackages();

        var dict = Extensions.ReflectFieldInfo(typeof(FileBackupView));
        Debug.WriteLine($"{nameof(FileBackupView)} is utilizing {dict.Count} object types.");
        #endregion

        #region [Testing support for a USB storage device]
        //_dwh = new DeviceWatcherHelper(DispatcherQueue.GetForCurrentThread(), ref _logMessages);
        //_ = _dwh.WatchDevices();
        //_dwh.LookupDevice("", "", @"\\?\USB#VID_0A12&PID_0001#6&268a6ccc&0&3#{0850302a-b344-4fda-9be9-90576b8d46f0}");
        //_dwh.LookupDevice("", "", @"\\?\HID#VID_046D&PID_C534&MI_00#8&2f0241a8&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}");
        #endregion

        #region [Show which color was selected for DesktopAcrylicController]
        if (AppSettings.Config.RandomBackdrop)
        {
            var possibilities = GetWinUIColorList();
            var found = possibilities?.Any(obj => obj.Color == MainWindow.BackdropColor);
            if (found.HasValue && found.Value)
            {
                var match = possibilities?.FirstOrDefault(obj => obj.Color == MainWindow.BackdropColor);
                Write($"Backdrop color is {match.KeyName} ({match.HexCode})", LogLevel.Debug);
            }
            else
            {
                Write($"Backdrop color is {MainWindow.BackdropColor}", LogLevel.Debug);
            }
        }
        #endregion

        #region [Deferred Logging System]
        ConfigureThreadPoolTimer(() =>
        {
            CheckLoggingQueue();
            // We'll also using this timer to update our stats.
            TotalCPU = $"{GetCPUTime()} ({cpuUsage})";
            MemoryCount = GetMemoryCommit();
        },
        () =>
        {
            Debug.WriteLine($"ThreadPoolTimer cancelled.");
        },
        TimeSpan.FromSeconds(4));
        #endregion

        // Auto-select the repo path once loaded.
        tbPath.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => 
        { 
            tbPath.Focus(FocusState.Programmatic);
            tbPath.SelectAll();
        });

        // Signal for double-trigger events.
        _startupFinished = true;
    }
    static bool cpuRunning = true;
    static string cpuUsage = "";

    /// <summary>
    /// <see cref="ILogger"/> error event.
    /// </summary>
    /// <param name="obj"><see cref="Exception"/></param>
    void LoggerOnException(Exception obj)
    {
        Write($"Logger: {obj.Message}", LogLevel.Notice);
    }

    /// <summary>
    /// <see cref="Pivot"/> control event.
    /// </summary>
    void settingPivotOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PivotItem? selectedPivot = settingPivot.SelectedItem as PivotItem;

        // Make the active pivot image colored, and inactive items grayed.
        // You could also do this with the VisualStateManager.
        foreach (PivotItem item in settingPivot.Items)
        {
            if (item == selectedPivot)
            {
                var header = item.Header as Controls.TabHeader;
                if (header != null)
                    header.SetSelectedItem(true);
            }
            else
            {
                var header = item.Header as Controls.TabHeader;
                if (header != null)
                    header.SetSelectedItem(false);
            }
        }
    }

    /// <summary>
    /// Cancel anything that's still running.
    /// </summary>
    void AppOnWindowClosing(string obj)
    {
        if (_dwh != null)
			_dwh.Dispose();
        
        if (_debounceTimer != null && _debounceTimer.IsRunning)
            _debounceTimer.Stop();

		_periodicTimer?.Cancel();
        _cts?.Cancel();
        cpuRunning = false;
        AppSettings.SaveSettings();
    }

    /// <summary>
    /// <see cref="Button"/> event.
    /// </summary>
    void btnCancelOnClick(object sender, RoutedEventArgs e)
    {
        if (IsBusy && _cts != null && _cts.Token.CanBeCanceled)
        {
            NotifyInfoBar($"Cancellation requested!", InfoBarSeverity.Warning);
            _cts.Cancel();
        }
    }

    /// <summary>
    /// <see cref="Button"/> event.
    /// </summary>
    async void btnEditIndexDatabaseOnClick(object sender, RoutedEventArgs e)
    {
        //Windows.UI.Notifications.TileUpdateManager.CreateTileUpdaterForApplication


        // How to get the selection rect of the button pressed.
        var rect = (sender as FrameworkElement)?.GetElementRect();

        string workPath = tbPath.Text;
        if (string.IsNullOrEmpty(workPath))
        {
            NotifyInfoBar($"You must supply a working path.", InfoBarSeverity.Warning);
            return;
        }

        IsBusy = true;

        try
        {
            if (workPath.EndsWith("\\"))
                workPath = workPath.Substring(0, workPath.Length - 1); // for proper Path.GetDirectoryName()

            string? rootPath = Path.GetDirectoryName(workPath);
            string dbTitle = "rb_" + workPath.Replace(rootPath ?? "Unknown", "").Replace("\\", "");

            string storeDB = Path.Combine(GetDefaultFolder(), dbTitle + ".idb");

            if (File.Exists(storeDB))
            {
                Write($"Opening '{storeDB}'", LogLevel.Info);
                try
                {
                    var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = storeDB,
                        UseShellExecute = true, /* very important */
                        WorkingDirectory = GetDefaultFolder(),
                    });
                    // We don't want the user inadvertently locking our IDB
                    // so wait here until they close the editing application.
                    p?.WaitForExit();
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("No application is associated"))
                    {
                        Write($"Confirm that an editor is associated with the 'idb' file extension.", LogLevel.Notice);
                        // Reveal the file location and let the user decide.
                        Process.Start($"explorer.exe", $"/select,{storeDB}");
                    }
                }
            }
            else
            {
                Write($"'{storeDB}' does not exist", LogLevel.Warning);
                ShowDialog($"'{storeDB}' does not exist");
            }
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
            
            // Show a small dialog with the error.
            ContentDialog warningDialog = new ContentDialog()
            {
                XamlRoot = (sender as Button)?.XamlRoot,
                Title = "Warning",
                Content = $"\n{ex.Message}",
                PrimaryButtonText = "OK"
            };
            await warningDialog.ShowAsync();
        }

        IsBusy = false;
    }

    /// <summary>
    /// <see cref="Button"/> event.
    /// </summary>
    void btnBackupOnClick(object sender, RoutedEventArgs e)
    {
        string bufferPath = tbBuffer.Text;
        string workPath = tbPath.Text;
        if (!Directory.Exists(workPath))
        {
            NotifyInfoBar($"Path does not exist.", InfoBarSeverity.Warning);
            return;
        }
        if (!Directory.Exists(bufferPath))
        {
            NotifyInfoBar($"Backup folder '{bufferPath}' does not exist.", InfoBarSeverity.Warning);
            return;
        }

        IsBusy = true;
        ResetState();

        if (workPath.EndsWith("\\"))
            workPath = workPath.Substring(0, workPath.Length - 1); // for proper Path.GetDirectoryName()

        string? rootPath = Path.GetDirectoryName(workPath);
        string dbTitle = "rb_" + workPath.Replace(rootPath ?? "Unknown", "").Replace("\\","");

        string msg = $"Performing {(AppSettings.Config.AtWork ? "work" : "home")} backup.";
        NotifyInfoBar(msg, InfoBarSeverity.Informational);
        _logQueue.Enqueue(new LogEntry { Message = $"{msg} Path='{workPath}'", Method = $"BackupOnClick", Severity = LogLevel.Info, Time = DateTime.Now });

        _cts = new CancellationTokenSource();
        _elapsed = ValueStopwatch.StartNew();

        // Delegate backup process, so we have responsive UI.
        Task.Run(async delegate () 
        {
            await IndexProcess(dbTitle, workPath, bufferPath, _cts.Token);

        }).GetAwaiter().OnCompleted(() => 
        {
            IsBusy = false;
            UpdateProgressBar(100);
            Progress = $"";

            if (_cts.Token.IsCancellationRequested)
            {
                msg = $"Backup attempt interrupted due to cancellation. Process lasted {_elapsed.GetElapsedTime().ToReadableTime()}";
                NotifyInfoBar(msg, InfoBarSeverity.Warning);
                _logQueue.Enqueue(new LogEntry { Message = msg, Method = $"IndexProcess", Severity = LogLevel.Important, Time = DateTime.Now });
				ToastImageAndText(msg);
			}
			else if (string.IsNullOrEmpty(_lastError))
            {
                msg = $"Backup attempt completed without issue. Process lasted {_elapsed.GetElapsedTime().ToReadableTime()}";
                NotifyInfoBar(msg, InfoBarSeverity.Success);
                _logQueue.Enqueue(new LogEntry { Message = msg, Method = $"IndexProcess", Severity = LogLevel.Success, Time = DateTime.Now });
                UpdateMRU(workPath, totalFilesInspected);
                ToastImageAndText(msg);
                tbPath.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => { tbPath.Focus(FocusState.Programmatic); });
            }
            else
            {
                msg = $"Backup attempt completed with issues. Process lasted {_elapsed.GetElapsedTime().ToReadableTime()}";
                NotifyInfoBar(msg, InfoBarSeverity.Error);
                _logQueue.Enqueue(new LogEntry { Message = msg, Method = $"IndexProcess", Severity = LogLevel.Warning, Time = DateTime.Now });
                UpdateMRU(workPath, totalFilesInspected);
                ShowDialog(_lastError);
            }
        });
    }

    /// <summary>
    /// <see cref="TextBox"/> keypress event.
    /// </summary>
    void TextBoxOnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var textbox = sender as TextBox;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            btnBackupOnClick(this, new RoutedEventArgs());
            e.Handled = true; // mark that we've handled it
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            if ((MostRecent.Count > 0) && (currentPathSelection > 0) && (currentPathSelection < MostRecent.Count))
                tbPath.Text = MostRecent[--currentPathSelection].Folder;

            e.Handled = true; // mark that we've handled it
        }
        else if (e.Key == Windows.System.VirtualKey.Down)
        {
            if ((MostRecent.Count > 0) && (currentPathSelection < (MostRecent.Count-1)))
                tbPath.Text = MostRecent[++currentPathSelection].Folder;

            e.Handled = true; // mark that we've handled it
        }
        else if (e.Key == Windows.System.VirtualKey.F2)
        {
            if (Random.Shared.Next(1,3) == 2)
                TestNoticeDialog(sender, e);
            else
                TestGenericDialog(sender, e);
        }

        Debug.WriteLine($"User pressed the [{e.Key}] key.");
    }
    int currentPathSelection = 0;

    /// <summary>
    /// <see cref="SplitView"/> event.
    /// </summary>
    void OnPaneOpenedOrClosed(SplitView sender, object args)
    {
        //Write($"{sender.Name} was {((SettingsSplitView.IsPaneOpen) ? "opened" : "closed")}", LogLevel.Info);

        // TODO: Add theme config support.
        //string currentTheme = sender.RequestedTheme == ElementTheme.Default ? App.Current.RequestedTheme.ToString() : sender.RequestedTheme.ToString();

        if (!SettingsSplitView.IsPaneOpen)
        {
            if (cbWorkLocation.IsChecked.HasValue && cbWorkLocation.IsChecked.Value && !string.IsNullOrEmpty(tbBuffer.Text))
                AppSettings.Config.WorkBufferFolder = tbBuffer.Text;
            else if (cbWorkLocation.IsChecked.HasValue && !cbWorkLocation.IsChecked.Value && !string.IsNullOrEmpty(tbBuffer.Text))
                AppSettings.Config.HomeBufferFolder = tbBuffer.Text;

            if (App.AnimationsEffectsEnabled)
                StoryboardPath.Stop();
        }
        else
        {
            if (App.AnimationsEffectsEnabled)
                StoryboardPath.Begin();
        }
    }

    /// <summary>
    /// Button event.
    /// </summary>
    void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsSplitView.IsPaneOpen = !SettingsSplitView.IsPaneOpen;
    }

    /// <summary>
    /// Custom event for shifting focus back to TextBox after TeachingTip is opened.
    /// https://github.com/microsoft/microsoft-ui-xaml/issues/3257
    /// </summary>
    void IsTipOpenChanged(DependencyObject sender, DependencyProperty dp)
    {
        // The main methods for DependencyObject are GetValue(), SetValue() and ClearValue().
        var prop = sender.GetValue(dp);

        // We know the property is a boolean, but here's an example of how to query the DP:
        if (prop is bool iop && iop == true)
            Debug.WriteLine($"DependencyProperty is true.");

        if (dp == TeachingTip.IsOpenProperty)
            Debug.WriteLine($"DependencyProperty is a TeachingTip.IsOpenProperty");

        // Now do what we came here to do...
        var tt = sender as TeachingTip;
        if (tt != null && tt.IsOpen)
        {
            _tipShown = true;
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(4.0d);
            _autoCloseTimer.Tick += (_, _) =>
            {
                try
                {
                    tt.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => { tt.IsOpen = false; });
                    if (_autoCloseTimer != null) { _autoCloseTimer.Stop(); }
                }
                catch (Exception)
                {
                    Debug.WriteLine($"Application may be in the process of closing.");
                }
            };
            _autoCloseTimer.Start();
            tbPath.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => { tbPath.Focus(FocusState.Programmatic); });
        }
    }

    /// <summary>
    /// Once the TextBox is clicked the TeachingTip will steal the focus and then our
    /// IsTipOpenedChanged callback will put it back, you will see the following...
    /// FocusState => Pointer
    /// FocusState => Unfocused
    /// FocusState => Pointer
    /// </summary>
    void IsFocusStateChanged(DependencyObject sender, DependencyProperty dp)
    {
        var tb = sender as TextBox;
        Debug.WriteLine($"FocusState => {tb?.FocusState}");
    }

    /// <summary>
    /// <see cref="TextBox"/> event.
    /// </summary>
    void tbPathOnGotFocus(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && ViewModel.ShowFrame)
            return;
        else if (!teachingTip.IsOpen && !_tipShown)
            teachingTip.IsOpen = true;
    }

    /// <summary>
    /// <see cref="CheckBox"/> event.
    /// </summary>
    void cbWorkLocationChecked(object sender, RoutedEventArgs e)
    {
        if (!_startupFinished) { return; }

        AppSettings.Config.AtWork = cbWorkLocation.IsChecked ?? false;

        if (AppSettings.Config.AtWork)
            tbBuffer.Text = AppSettings.Config.WorkBufferFolder;
        else
            tbBuffer.Text = AppSettings.Config.HomeBufferFolder;

        // Try to cut back on extra typing.
        if (AppSettings.Config.AtWork && tbPath.Text.Contains(AppSettings.Config.HomeRepoFolder, StringComparison.OrdinalIgnoreCase))
            tbPath.Text = tbPath.Text.Replace(AppSettings.Config.HomeRepoFolder, AppSettings.Config.WorkRepoFolder, StringComparison.OrdinalIgnoreCase);
        else if (!AppSettings.Config.AtWork && tbPath.Text.Contains(AppSettings.Config.WorkRepoFolder, StringComparison.OrdinalIgnoreCase))
            tbPath.Text = tbPath.Text.Replace(AppSettings.Config.WorkRepoFolder, AppSettings.Config.HomeRepoFolder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <see cref="CheckBox"/> event.
    /// </summary>
    void cbOpenExplorerChecked(object sender, RoutedEventArgs e)
    {
        if (!_startupFinished) { return; }
        AppSettings.Config.ExplorerShell = cbOpenExplorer.IsChecked ?? false;
    }

    /// <summary>
    /// <see cref="CheckBox"/> event.
    /// </summary>
    void cbBackupInitialChecked(object sender, RoutedEventArgs e)
    {
        if (!_startupFinished) { return; }
        AppSettings.Config.FullInitialBackup = cbBackupInitial.IsChecked ?? false;
    }

    /// <summary>
    /// <see cref="CheckBox"/> event.
    /// </summary>
    void cbRandomBackdropChecked(object sender, RoutedEventArgs e)
    {
        if (!_startupFinished) { return; }
        AppSettings.Config.RandomBackdrop = cbRandomBackdrop.IsChecked ?? false;
    }

    /// <summary>
    /// <see cref="ComboBox"/> event.
    /// </summary>
    void OnThreadSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_startupFinished) { return; }
        var cb = sender as ComboBox;
        if (cb != null)
            AppSettings.Config.ThreadIndex = cb.SelectedIndex; //(int)cb.SelectedValue;
    }

    /// <summary>
    /// <see cref="ComboBox"/> event.
    /// </summary>
    void OnSpoiledSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_startupFinished) { return; }
        var cb = sender as ComboBox;
        if (cb != null)
            AppSettings.Config.StaleIndex = cb.SelectedIndex; //(int)cb.SelectedValue;
    }

    /// <summary>
    /// I was doing this in the XAML, but this resize event is more accurate.
    /// [Rectangle]
    /// Width="{Binding ElementName=SettingsSplitView, Path=ActualWidth, Mode=OneWay, Converter={StaticResource SizeIncrease}, ConverterParameter=1.085}"
    /// Height="{Binding ElementName=SettingsSplitView, Path=ActualHeight, Mode=OneWay, Converter={StaticResource SizeIncrease}, ConverterParameter=1.15}"
    /// </summary>
    void FileBackupViewOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_debounceTimer == null)
            _debounceTimer = DispatcherQueue.CreateTimer();

        // The resize event will fire many times, so we'll only execute this
        // code after some amount of time has elapsed since the last trigger.
        _debounceTimer?.Debounce(async () =>
        {
            Debug.WriteLine($"[Debounce OnSizeChanged {DateTime.Now.ToString("hh:mm:ss.fff tt")}]");
            if (e.NewSize.Height > 0)
            {
                AcrylicRectangle.Height = e.NewSize.Height * 1.15d; // force the scroll so we can see the effect
            }

        }, TimeSpan.FromSeconds(0.5));
    }

    /// <summary>
    /// We're using this to auto-select the latest item that was added into the list.
    /// </summary>
    void MessagesOnCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            //lvMessages.ScrollIntoView(lvMessages.Items.Count);
            DispatcherQueue.TryEnqueue(() =>
            {
                int selectedIndex = lvMessages.Items.Count - 1;
                if (selectedIndex < 0) { return; }
                lvMessages.SelectedIndex = selectedIndex;
                lvMessages.UpdateLayout();
                lvMessages.ScrollIntoView(lvMessages.SelectedItem);
            });
        }
    }

    /// <summary>
    /// https://mikaelkoskinen.net/post/winrt-xaml-automatically-scrolling-listview-to-bottom-and-detecting-when-listview-is-scrolled
    /// </summary>
    void VerticalBarScroll(object sender, ScrollEventArgs e)
    {
        if (e.ScrollEventType != ScrollEventType.EndScroll)
            return;

        var bar = sender as ScrollBar;
        if (bar == null)
            return;

        Debug.WriteLine("Scrolling ended");

        if (e.NewValue >= bar.Maximum)
        {
            Debug.WriteLine("We are at the bottom");
            LockToBottom = true;
        }
        else
        {
            Debug.WriteLine("We are away from the bottom");
            LockToBottom = false;
        }
    }
    bool LockToBottom = false;

    /// <summary>
    /// https://mikaelkoskinen.net/post/winrt-xaml-automatically-scrolling-listview-to-bottom-and-detecting-when-listview-is-scrolled
    /// </summary>
    public void ScrollToBottom()
    {
        if (!LockToBottom) { return; }

        DispatcherQueue.TryEnqueue(() =>
        {
            int selectedIndex = lvMessages.Items.Count - 1;
            if (selectedIndex < 0) { return; }
            lvMessages.SelectedIndex = selectedIndex;
            lvMessages.UpdateLayout();
            lvMessages.ScrollIntoView(lvMessages.SelectedItem);

            // -- Or --
            //ScrollViewer scrollViewer = lvMessages.GetFirstDescendantOfType<ScrollViewer>();
            //scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
        });

    }
    #endregion

    #region [Command Methods]
    /// <summary>
    /// <see cref="RelayCommand"/> target.
    /// </summary>
    /// <param name="ctrl">passed parameter</param>
    async Task OpenAbout(object? ctrl)
    {
        if (ctrl is MenuFlyoutItem mfi)
            Write($"Received command parameter from '{mfi?.Name ?? "N/A"}'", LogLevel.Debug);

        try
        {
            var data = Extensions.GatherLoadedModules(true);
            if (string.IsNullOrEmpty(data)) { return; }
            tbAssemblies.Text = data;
            contentDialog.XamlRoot = App.MainRoot?.XamlRoot;
            await contentDialog.ShowAsync();
        }
        catch (Exception ex) { Write(ex.Message, LogLevel.Error); }
    }

    /// <summary>
    /// <see cref="RelayCommand"/> target.
    /// </summary>
    /// <param name="ctrl">passed parameter</param>
    void OpenLogFile(object? ctrl)
    {
        if (Logger == null)
            return;

        if (ctrl is MenuFlyoutItem mfi)
            Write($"Received command parameter from '{mfi?.Name ?? "N/A"}'", LogLevel.Debug);
        else if (ctrl is string str)
            Write($"Received command parameter '{str}'", LogLevel.Debug);

        Write($"Opening log file", LogLevel.Info);
        string fileName = Logger.GetCurrentLogPath();
        if (System.IO.File.Exists(fileName))
        {
            try
            {
                Task.Run(() => 
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        UseShellExecute = true // very important! 
                    };
                    Process.Start(startInfo);
                });
            }
            catch (Exception ex) { Write(ex.Message, LogLevel.Error); }
        }
        else 
        { 
            Write($"'{System.IO.Path.GetFileName(fileName)}' does not exist", LogLevel.Warning); 
        }
    }

    /// <summary>
    /// <see cref="RelayCommand"/> target.
    /// </summary>
    /// <param name="obj">passed parameter</param>
    void TestCommandParameter(object? obj)
    {
        if (obj is MenuFlyoutItem mfi)
            Write($"Received command parameter from '{mfi?.Name ?? "N/A"}'", LogLevel.Debug);
        else if (obj is string str)
            Write($"Received command parameter '{str}'", LogLevel.Debug);
        else if (obj is Int32 val)
            Write($"Received command parameter '{val}'", LogLevel.Debug);
        else if (obj is Boolean bl)
            Write($"Received command parameter '{bl}'", LogLevel.Debug);
        else if (obj is Windows.UI.Color clr)
            Write($"Received command parameter '{clr}'", LogLevel.Debug);
        else
            Write($"Command parameter type is not defined: {obj?.NameOf()}", LogLevel.Debug);
    }
    #endregion

    #region [Tile Routines]
    /// <summary>
    /// Currently these calls do not work as expected under WinUI3 apps.
    /// https://learn.microsoft.com/en-us/uwp/api/windows.ui.startscreen.secondarytile?view=winrt-22621
    /// </summary>
    async Task PinTile(string DisplayName)
    {
        try
        {
            // Get all secondary tiles
            var tiles = await SecondaryTile.FindAllAsync();
            foreach (var t in tiles)
                Debug.WriteLine($"Tile Name: {t.DisplayName}");

            var tileId = DateTime.Now.Ticks.ToString();
            SecondaryTile tile = new SecondaryTile(tileId, DisplayName, "args", new Uri("ms-appx:///Assets/RepoFolderSmall.png"), TileSize.Default);
            tile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/RepoFolderTiny.png");
            tile.VisualElements.Square150x150Logo = new Uri("ms-appx:///Assets/RepoFolderSmall.png");
            tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/RepoFolderMedium.png");
            tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/RepoFolderBig.png");
            tile.VisualElements.ShowNameOnSquare150x150Logo = true;
            //tile.VisualElements.ShowNameOnSquare310x310Logo = true;
            //tile.VisualElements.ShowNameOnWide310x150Logo = true;

            await tile.RequestCreateAsync();
        }

        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(async () => { await App.MainRoot.MessageDialogAsync("Notice", $"{ex}"); });
        }
    }
    #endregion

    #region [JumpList Routines]
    /// <summary>
    /// See if the JumpList is supported and if we need to add an item to list.
    /// This is just for demo purposes and has not been fully implemented.
    /// </summary>
    async Task CheckJumpList()
    {
        try
        {
            if (!JumpList.IsSupported())
            {
                Write($"JumpList is not supported on this platform", LogLevel.Notice);
                return;
            }

            JumpList? jumpList = await JumpList.LoadCurrentAsync();
            if (jumpList.Items.Count == 0)
                await AddJumpList();
            else
                Debug.WriteLine($"JumpList is already established");
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// When invoked from the taskbar the application will restart itself with the 
    /// default path of "C:\Windows\System32". This needs to be addressed to 
    /// account for the loading of the "MRU.xml" and "Settings.xml" files.
    /// </summary>
    async Task AddJumpList()
    {
        try
        {
            JumpList? jumpList = await JumpList.LoadCurrentAsync();
            jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
            
            JumpListItem? item = JumpListItem.CreateWithArguments($"-backup {RepoPath}", "Perform Backup");
            if (item != null)
            {
                item.GroupName = "Jump Items";
                item.Description = "Run a backup now";
                item.Logo = new Uri("ms-appx:///Assets/Searcher.png");
                jumpList.Items.Add(item);
            }

            JumpListItem? sepa = JumpListItem.CreateSeparator();
            if (sepa != null)
                jumpList.Items.Add(sepa);

            await jumpList.SaveAsync();
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>
    /// Clears the JumpList items list.
    /// </summary>
    async Task RemoveJumpList()
    {
        try
        {
            JumpList? jumpList = await JumpList.LoadCurrentAsync();
            jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
            jumpList.Items.Clear();
            await jumpList.SaveAsync();
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
        }
    }
    #endregion

    #region [Logger Routines]
    /// <summary>
    /// We could use a regular <see cref="System.Threading.Timer"/> for this, but I thought it might
    /// be interesting to show off the under-used <see cref="Windows.System.Threading.ThreadPoolTimer"/>.
    /// https://learn.microsoft.com/en-us/windows/uwp/threading-async/create-a-periodic-work-item
    /// </summary>
    /// <remarks>
    /// <see cref="ThreadPoolTimer.CreateTimer"/> happens only once.
    /// <see cref="ThreadPoolTimer.CreatePeriodicTimer"/> happens again and again.
    /// A <see cref="System.TimeSpan"/> value of zero will cause the periodic timer to behave as a single-shot timer.
    /// </remarks>
    void ConfigureThreadPoolTimer(Action workToDo, Action afterCancellation, TimeSpan period)
    {
        // Have we already initialized the timer?
        if (_periodicTimer != null || App.IsClosing)
            return;

        _periodicTimer = ThreadPoolTimer.CreatePeriodicTimer((source) =>
        {
            try { workToDo(); }
            catch (Exception ex) { Logger?.WriteLine($"TimerElapsedHandler: {ex.Message}", LogLevel.Error); }
        },
        period,
        (source) =>
        {
            try { afterCancellation(); }
            catch (Exception ex) { Logger?.WriteLine($"TimerDestroyedHandler: {ex.Message}", LogLevel.Error); }
        });
    }

    /// <summary>
    /// To be called from a timer routine.
    /// </summary>
    void CheckLoggingQueue()
    {
        lock (_logQueue)
        {
            while (_logQueue.Count > 0)
            {
                var item = _logQueue.Dequeue();
                if (item != null && Logger != null && item.Severity != LogLevel.Off)
                    Logger.WriteLine($"{item.Message}", item.Severity, !string.IsNullOrEmpty(item.Method) ? item.Method : "");
            }
        }
    }
    #endregion

    #region [Serializer Routines]
    /// <summary>
    /// Loads the most recently used collection.
    /// </summary>
    void LoadMRU()
    {
        string baseFolder = "";

        try
        {
            if (App.IsPackaged)
                baseFolder = ApplicationData.Current.LocalFolder.Path;
            else
                baseFolder = Directory.GetCurrentDirectory();

            if (File.Exists(Path.Combine(baseFolder, @"MRU.xml")))
            {
                var data = File.ReadAllText(Path.Combine(baseFolder, @"MRU.xml"));
                var serializer = new XmlSerializer(typeof(List<MRU>));
                if (serializer != null)
                {
                    MostRecent = serializer.Deserialize(new StringReader(data)) as List<MRU> ?? GenerateDefaultMRU();
                }
                else
                {
                    Logger?.WriteLine($"XmlSerializer was null.", LogLevel.Warning);
                }
            }
            else
            {   // Inject some dummy data if file was not found.
                MostRecent = GenerateDefaultMRU();
                SaveMRU();
            }
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"{ex.Message}", LogLevel.Error);
            Debug.WriteLine($"LoadMRU: {ex.Message}");
            Debugger.Break();
        }
    }

    /// <summary>
    /// Saves the most recently used collection.
    /// </summary>
    public void SaveMRU()
    {
        string baseFolder = "";

        try
        {
            if (App.IsPackaged)
                baseFolder = ApplicationData.Current.LocalFolder.Path;
            else
                baseFolder = Directory.GetCurrentDirectory();

            var serializer = new XmlSerializer(typeof(List<MRU>));
            if (serializer != null)
            {
                var stringWriter = new StringWriter();
                serializer.Serialize(stringWriter, MostRecent);
                var applicationData = stringWriter.ToString();
                File.WriteAllText(Path.Combine(baseFolder, @"MRU.xml"), applicationData);
            }
            else
            {
                Logger?.WriteLine($"XmlSerializer was null.", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"{ex.Message}", LogLevel.Error);
            Debug.WriteLine($"SaveMRU: {ex.Message}");
            Debugger.Break();
        }
    }

    /// <summary>
    /// Adds an attempt to the MRU history.
    /// </summary>
    void UpdateMRU(string folderPath, int fileCount)
    {
        var existing = MostRecent.Select(o => o).Where(m => m.Folder.Equals(folderPath, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        if (existing != null)
        {
            // Remove existing one and replace with updated one.
            var idx = MostRecent.IndexOf(existing);
            MostRecent.RemoveAt(idx);
            MostRecent.Insert(idx, new MRU { Folder = folderPath, Time = DateTime.Now, Count = fileCount });
        }
        else
        {
            MostRecent.Add(new MRU { Folder = folderPath, Time = DateTime.Now, Count = fileCount });
        }
        SaveMRU();
    }

    /// <summary>
    /// Creates a new <see cref="List{MRU}"/> object with example data.
    /// </summary>
    /// <returns><see cref="List{MRU}"/></returns>
    List<MRU> GenerateDefaultMRU()
    {
        return new List<MRU>
        {
            new MRU { Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos\WinUI"), Time = DateTime.Now, Count = 12345 },
            new MRU { Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos"), Time = DateTime.Now.AddDays(-15), Count = 67000 },
        };
    }
    /*
    /// <summary>
    /// Loads the application settings collection.
    /// </summary>
    void LoadSettings()
    {
        string baseFolder = "";

        try
        {
            if (App.IsPackaged)
                baseFolder = ApplicationData.Current.LocalFolder.Path;
            else
                baseFolder = Directory.GetCurrentDirectory();

            if (File.Exists(Path.Combine(baseFolder, @"Settings.xml")))
            {
                var data = File.ReadAllText(Path.Combine(baseFolder, @"Settings.xml"));
                var serializer = new XmlSerializer(typeof(Settings));
                if (serializer != null)
                {
                    Config = serializer.Deserialize(new StringReader(data)) as Settings ?? GenerateDefaultSettings();
                }
                else
                {
                    Logger?.WriteLine($"XmlSerializer was null.", LogLevel.Warning);
                }
            }
            else
            {
                // Create a default config if not found.
                Config = GenerateDefaultSettings();
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"{ex.Message}", LogLevel.Error);
            Debug.WriteLine($"LoadSettings: {ex.Message}");
            Debugger.Break();
        }
    }

    /// <summary>
    /// Saves the application settings collection.
    /// </summary>
    public void SaveSettings()
    {
        string baseFolder = "";

        try
        {
            if (App.IsPackaged)
                baseFolder = ApplicationData.Current.LocalFolder.Path;
            else
                baseFolder = Directory.GetCurrentDirectory();

            var serializer = new XmlSerializer(typeof(Settings));
            if (serializer != null)
            {
                var stringWriter = new StringWriter();
                serializer.Serialize(stringWriter, Config);
                var applicationData = stringWriter.ToString();
                File.WriteAllText(Path.Combine(baseFolder, @"Settings.xml"), applicationData);
            }
            else
            {
                Logger?.WriteLine($"XmlSerializer was null.", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            Logger?.WriteLine($"{ex.Message}", LogLevel.Error);
            Debug.WriteLine($"SaveSettings: {ex.Message}");
            Debugger.Break();
        }
    }

    /// <summary>
    /// Creates a new <see cref="Settings"/> object.
    /// </summary>
    /// <returns><see cref="Settings"/></returns>
    Settings GenerateDefaultSettings()
    {
        return new Settings
        {
            Theme = "Dark",
            AtWork = false,
            ExplorerShell = true,
            FullInitialBackup = false,
            StaleIndex = 6,
            ThreadIndex = Math.Min(ThreadCount.Count - 1, GetProcessorCount() / 4),
            HomeRepoFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos"),
            HomeBufferFolder = @"F:\RepoBackups",
            WorkRepoFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos"),
            WorkBufferFolder = @"D:\RepoBackups",
        };
    }
    */
    #endregion

    #region [Thread-safe UI Helpers]
    void RunOnUI(Action action)
    {
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            try { action(); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        });
    }

    void Write(string message)
    {
        AddMessage(message.Truncate(42, message.GetLast(42)));
    }

    void Write(string message, LogLevel severity)
    {
        AddMessage(message, severity);
    }

    void Write(string message, DateTime time, LogLevel severity)
    {
        AddMessage(message, time, severity);
    }

    /// <summary>
    /// Thread-safe ObservableCollection helper method.
    /// </summary>
    public void AddMessage(string message, LogLevel severity)
    {
        if (!App.IsClosing)
            DispatcherQueue.TryEnqueue(() => { lock (_obj) { LogMessages.Insert(0, new LogEntry { Message = message, Severity = severity, Time = DateTime.Now }); } });
    }

    /// <summary>
    /// Thread-safe ObservableCollection helper method.
    /// </summary>
    public void AddMessage(string message, DateTime time, LogLevel severity)
    {
        if (!App.IsClosing)
            DispatcherQueue.TryEnqueue(() => { lock (_obj) { LogMessages.Insert(0, new LogEntry { Message = message, Severity = severity, Time = time }); } });
    }

    /// <summary>
    /// Thread-safe ObservableCollection helper method.
    /// </summary>
    public void AddMessage(string strMessage, bool addTime = true, bool autoScroll = true)
    {
        if (App.IsClosing)
            return;

        string msg = addTime ? $"[{DateTime.Now.ToString("hh:mm:ss.fff tt")}] " + strMessage : strMessage;

        if (DispatcherQueue.HasThreadAccess)
        {
            lock (_obj) // we may get called from another semaphore thread
            {
                if (autoScroll)
                {
                    Messages.Add(msg);
                    
                    // This works fine, but we are now using the
                    // MessagesOnCollectionChanged to automatically
                    // highlight the last item in the ListView.
                    //lvMessages.ScrollIntoView(msg);
                }
                else
                {
                    Messages.Insert(0, msg);
                }
            }
        }
        else
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                lock (_obj) // we may get called from another semaphore thread
                {
                    if (autoScroll)
                    {
                        Messages.Add(msg);

                        // This works fine, but we are now using the
                        // MessagesOnCollectionChanged to automatically
                        // highlight the last item in the ListView.
                        //lvMessages.ScrollIntoView(msg);
                    }
                    else
                    {
                        Messages.Insert(0, msg);
                    }
                }
            });
        }
    }

    /// <summary>
    /// Thread-safe ProgressBar helper method.
    /// </summary>
    public void UpdateProgressBar(int newValue)
    {
        if (App.IsClosing)
            return;

        if (DispatcherQueue.HasThreadAccess)
            progBar.Value = newValue;
        else
            DispatcherQueue.TryEnqueue(() => { progBar.Value = newValue; });
    }

    /// <summary>
    /// Thread-safe InfoBar helper method.
    /// </summary>
    public void NotifyInfoBar(string strMessage, InfoBarSeverity severity, bool isOpen = true)
    {
        if (App.IsClosing)
            return;

        if (DispatcherQueue.HasThreadAccess)
            UpdateInfoBar(strMessage, severity, isOpen);
        else
            DispatcherQueue.TryEnqueue(() => { UpdateInfoBar(strMessage, severity, isOpen); });
    }

    /// <summary>
    /// The compliment to the <see cref="NotifyInfoBar"/> method.
    /// </summary>
    void UpdateInfoBar(string strMessage, InfoBarSeverity severity, bool isOpen)
    {
        if (App.IsClosing)
            return;

        infoBar.Message = $"{strMessage}";
        infoBar.Severity = severity;
        infoBar.IsOpen = isOpen; // Setting to false will close the InfoBar (similar to visibility).
        
        //infoBar.ActionButton = new HyperlinkButton()
        //{
        //    HorizontalAlignment = HorizontalAlignment.Right,
        //    Margin = new Thickness(10, 0, 0, 0),
        //    Content = "WinUI3",
        //    NavigateUri = new Uri("https://learn.microsoft.com/en-us/windows/apps/winui/winui3/")
        //};
    }

    /// <summary>
    /// Uses an awaitable extension to show a <see cref="ContentDialog"/>
    /// </summary>
    /// <param name="message">the text to display</param>
    async void ShowDialog(string message)
    {
        if (App.IsClosing)
            return;

        await App.MainRoot.MessageDialogAsync("Notice", message);
    }
    #endregion

    #region [AutoComplete Routines]
    /// <summary>
    /// Automatically search the directory tree once a valid path is established.
    /// </summary>
    void PathOnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = sender as TextBox;
        if (tb == null) { return; }

        if (!_evaluating && tb.Text.Length > 2 && System.IO.Directory.Exists(tb.Text))
        {
            // Extract the text before switching threads!
            string test = tb.Text;
            _evaluating = true;

            Task.Run(delegate ()
            {
                try
                {
                    string[] dirs = Directory.GetDirectories(test);
                    if (dirs.Length > 0)
                    {
                        lock (_obj)
                        {
                            AutoSuggestions.AddRange(dirs);
                        }

                        foreach (string dir in dirs)
                        {
                            // Do not use "SearchOption.AllDirectories" here...
                            foreach (string file in System.IO.Directory.GetFiles(dir, "*.sln", SearchOption.TopDirectoryOnly))
                            {
                                if (!AutoSuggestions.Contains(file))
                                {
                                    Debug.WriteLine($"[Adding Suggestion] '{file}'");
                                    lock (_obj)
                                    {
                                        AutoSuggestions.Add(file);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) // probably a permissions error
                {
                    NotifyInfoBar(ex.Message, InfoBarSeverity.Error);
                }
                finally
                {
                    _evaluating = false;
                }
            });
        }
    }
    #endregion

    #region [Backup Routines]
    static int matchCount = 0;
    static int lastCount = 0;
    static int progIdx = 0;
    static int transStarted = 0;
    static int progressMax = -1;
    static int progressCounter = 0;
    static int totalFilesInspected = 0;
    static readonly object lockObj = new object();

    /// <summary>
    /// Main calling for backup requests.
    /// </summary>
    /// <param name="title">name of backup</param>
    /// <param name="path">path to inspect</param>
    /// <param name="buffer">path for holding files to be compressed</param>
    /// <param name="token">cancellation token to be passed to subsequent callers</param>
    async Task IndexProcess(string title, string path, string buffer, CancellationToken token)
    {
        bool whole = false, part1 = false, part2 = false, processCanceled = false;

        Dictionary<string, string> stored = CheckIndexFreshness(title, path, token, StaleCount[AppSettings.Config.StaleIndex]);

        RemovePreviousBackup(buffer, token);

        CancellationTokenRegistration ctr = token.Register(() => 
        {
            processCanceled = true;
            Write($"Token registration invoked for '{title}'", LogLevel.Warning); 
        });

        TimeSpan dueTime = new TimeSpan(0, 0, 1); // Fire the first callback in 1 second.
        TimeSpan period = new TimeSpan(0, 0, 1);  // Fire additional callbacks every 1 second.

        // Is there something to work with?
        if (stored.Count() > 0)
        {
            progIdx = 0;
            transStarted = Environment.TickCount;
            Dictionary<string, string> current = new Dictionary<string, string>();

            using (System.Threading.Timer tmr = new(TimerProcCallback, null, dueTime, period)) // Create the progress timer.
            {
                Write($"Building index to compare", LogLevel.Info);
                current = IndexFolder(path, title, token, ThreadCount[AppSettings.Config.ThreadIndex]);

                Write($"Comparing elements…", LogLevel.Info);
                var comp = CompareIndexSets(current, stored);

                if (comp.Count() > 0)
                {
                    // We'll leave the timer running so we can see progress on the copy.
                    //tmr.Change(int.MaxValue, int.MaxValue);

                    if (!processCanceled)
                    {
                        Write("Performing backup", LogLevel.Info);
                        
                        // Setup two threads to do the copy process.
                        var (firstHalf, secondHalf) = comp.SplitDictionary();

                        // We'll split the copy operation into two parts in the event of a large repo.
                        Task[] tasks = new Task[2];

                        #region [Handle the first half]
                        tasks[0] = Task.Factory.StartNew(() => {
                            try
                            {
                                _logQueue.Enqueue(new LogEntry { Message = $"Starting copy task #1 ({firstHalf.Count} items)", Method = "IndexProcess", Severity = LogLevel.Debug, Time = DateTime.Now });
                                part1 = BackupFiles(buffer, firstHalf, token);
                            }
                            catch (AggregateException aex)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> AggregateException Caught! <<<");
                                aex?.Flatten().Handle(ex =>
                                {
                                    Write($"[{ex.GetType()}]: {ex.Message}", LogLevel.Error);
                                    return true; // don't re-throw
                                });
                            }
                            catch (TaskCanceledException)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> TaskCanceledException Caught! <<<");
                                Write($"TaskCanceledException[0]", LogLevel.Error);
                            }
                        },
                        token,
                        TaskCreationOptions.LongRunning,
                        SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Current);
                        #endregion

                        #region [Handle the second half]
                        tasks[1] = Task.Factory.StartNew(() => {
                            try
                            {
                                _logQueue.Enqueue(new LogEntry { Message = $"Starting copy task #2 ({secondHalf.Count} items)", Method = "IndexProcess", Severity = LogLevel.Debug, Time = DateTime.Now });
                                part2 = BackupFiles(buffer, secondHalf, token);
                            }
                            catch (AggregateException aex)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> AggregateException Caught! <<<");
                                aex?.Flatten().Handle(ex =>
                                {
                                    Write($"[{ex.GetType()}]: {ex.Message}", LogLevel.Error);
                                    return true; // don't re-throw
                                });
                            }
                            catch (TaskCanceledException)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> TaskCanceledException Caught! <<<");
                                Write($"TaskCanceledException[1]", LogLevel.Error);
                            }
                        },
                        token,
                        TaskCreationOptions.LongRunning,
                        SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Current);
                        #endregion

                        try
                        {   // Block until our two tasks are finished.
                            await Task.WhenAll(tasks);
                        }
                        catch (TaskCanceledException)
                        {
                            Write($"Copy tasks canceled", LogLevel.Warning);
                        }

                        // If everything went well then zip-up the files for convenience.
                        if (part1 && part2)
                        {
                            await Task.Delay(150);
                            if (ZipUpFiles(title + ".zip", buffer, token))
                            {
                                await Task.Delay(150);
                                SaveIndexDB(title, current);
                            }
                        }
                        else
                        {
                            Write("Backup was unsuccessful", LogLevel.Warning);
                        }
                    }
                    else
                    {
                        Write("Process canceled", LogLevel.Warning);
                        await Task.Delay(500);
                    }

                    Write($"Total files inspected: {totalFilesInspected.ToString("#,###,##0")}", LogLevel.Info);
                    Write($"Difference in count: {Math.Abs(progressMax - lastCount).ToString("#,###,##0")}", LogLevel.Info);
                    // Update our counter.
                    if (progressMax > 0)
                    {
                        //SaveConfig(Path.Combine(AppContext.BaseDirectory, $"{title}_config.txt"), new string[] { $"{totalFilesInspected}" });
                        UpdateMRU(path, totalFilesInspected);
                    }
                }
                else
                    Write($"Nothing to do", LogLevel.Notice);
            }
        }
        else // Initial indexing
        {   
            Write($"{title} db has no entries, creating new db", LogLevel.Important);
            using (System.Threading.Timer tmr = new(TimerProcCallback, null, dueTime, period)) // Create the progress timer.
            {
                var result = IndexFolder(path, title, token, ThreadCount[AppSettings.Config.ThreadIndex]);
                if (result.Count > 0)
                {
                    Write($"Total files inspected: {totalFilesInspected.ToString("#,###,##0")}", LogLevel.Info);
                    SaveIndexDB(title, result);
                    UpdateMRU(path, totalFilesInspected);
                    if (AppSettings.Config.FullInitialBackup && !processCanceled)
                    {
                        Write("Performing initial backup, this may take some time", LogLevel.Info);

                        // Setup two threads to do the copy process.
                        var (firstHalf, secondHalf) = result.SplitDictionary();

                        // We'll split the copy operation into two parts in the event of a large repo.
                        Task[] tasks = new Task[2];

                        #region [Handle the first half]
                        tasks[0] = Task.Factory.StartNew(() => {
                            try
                            {
                                _logQueue.Enqueue(new LogEntry { Message = $"Starting copy task #1 ({firstHalf.Count} items)", Method = "IndexProcess", Severity = LogLevel.Debug, Time = DateTime.Now });
                                part1 = BackupFiles(buffer, firstHalf, token);
                            }
                            catch (AggregateException aex)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> AggregateException Caught! <<<");
                                aex?.Flatten().Handle(ex =>
                                {
                                    Write($"[{ex.GetType()}]: {ex.Message}", LogLevel.Error);
                                    return true; // don't re-throw
                                });
                            }
                            catch (TaskCanceledException)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> TaskCanceledException Caught! <<<");
                                Write($"TaskCanceledException[0]", LogLevel.Error);
                            }
                        },
                        token,
                        TaskCreationOptions.LongRunning,
                        SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Current);
                        #endregion

                        #region [Handle the second half]
                        tasks[1] = Task.Factory.StartNew(() => {
                            try
                            {
                                _logQueue.Enqueue(new LogEntry { Message = $"Starting copy task #2 ({secondHalf.Count} items)", Method = "IndexProcess", Severity = LogLevel.Debug, Time = DateTime.Now });
                                part2 = BackupFiles(buffer, secondHalf, token);
                            }
                            catch (AggregateException aex)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> AggregateException Caught! <<<");
                                aex?.Flatten().Handle(ex =>
                                {
                                    Write($"[{ex.GetType()}]: {ex.Message}", LogLevel.Error);
                                    return true; // don't re-throw
                                });
                            }
                            catch (TaskCanceledException)
                            {   // If we don't catch these here then they'll be propagated up to the calling task.
                                Debug.WriteLine(">>> TaskCanceledException Caught! <<<");
                                Write($"TaskCanceledException[1]", LogLevel.Error);
                            }
                        },
                        token,
                        TaskCreationOptions.LongRunning,
                        SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Current);
                        #endregion

                        try
                        {   // Block until our two tasks are finished.
                            await Task.WhenAll(tasks);
                        }
                        catch (TaskCanceledException)
                        {
                            Write($"Copy tasks canceled", LogLevel.Warning);
                        }

                        // If everything went well then zip-up the files for convenience.
                        if (part1 && part2)
                        {
                            await Task.Delay(150);
                            _ = ZipUpFiles(title + ".zip", buffer, token);
                        }
                        else
                        {
                            Write("Backup was unsuccessful", LogLevel.Warning);
                        }
                    }
                    else
                    {
                        Write("Full initial backup skipped", LogLevel.Notice);
                        await Task.Delay(500);
                    }
                }
                else
                    Write($"Nothing to do", LogLevel.Notice);
            }
        }

        // If the registration was never used we'll need to
        // dispose so it doesn't hang around in memory forever.
        ctr.Dispose();
    }

    /// <summary>
    /// Indexing method to be called by multiple threads.
    /// </summary>
    void SemaphoreSearch(string dir, SemaphoreSlim semaphore, CancellationToken cts, ref Dictionary<string, string> indexDB, List<string> ignores)
    {
        bool isCompleted = false;
        while (!isCompleted && !cts.IsCancellationRequested)
        {
            try
            {
                if (semaphore.Wait(5000, cts)) // Will be true if the current thread was allowed to enter the semaphore.
                {
                    int? currId = Task.CurrentId;
                    try
                    {
                        // Check for intermediate files.
                        var ignore = ignores.Where(i => dir.Contains(i)).Any();
                        if (ignore)
                        {
                            // We won't waste time counting the files in these folders,
                            // just increment a few times for the progress estimation.
                            var newAtomic = Interlocked.Increment(ref totalFilesInspected) + 50;
                            Interlocked.Exchange(ref totalFilesInspected, newAtomic);
                        }
                        else
                        {
                            if (Extensions.IsValidPath(dir))
                            {
                                var results = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                                    .Where(filePath => Extensions.IsValidPath(filePath))
                                    .Select(f => f);

                                foreach (var fi in results)
                                {
                                    if (cts.IsCancellationRequested)
                                        break;

                                    Interlocked.Increment(ref totalFilesInspected);

                                    ignore = ignores.Where(i => fi.Contains(i)).Any();
                                    if (ignore)
                                    {
                                        // We should never get here, but there could be a sitch where a "bin" folder
                                        // contains another "obj" folder, so just ignore these intermediates.
                                    }
                                    else
                                    {
                                        // Check the path one more time since the addition
                                        // of a file name could put us over the edge.
                                        if (Extensions.IsValidPath(fi))
                                        {
                                            // Assemble file information for hashing.
                                            var name = Path.GetFileName(fi);
                                            var info = new FileInfo(fi);
                                            // The LastWriteTimeUtc may or may not be needed as some networked
                                            // locations can modify the write time based on when the files
                                            // were copied/moved which can result in a false-positive.
                                            string toHash = $"{name}*{info.LastWriteTimeUtc}*{info.Directory}*{info.Length}";
                                            var hashed = CRC32.Compute(toHash).ToString("X8");
                                            lock (lockObj)
                                            {
                                                indexDB.Add(fi, hashed);
                                            }
                                        }
                                        else
                                        {
                                            Write($"Bad path: '{fi}'", LogLevel.Warning);
                                        }
                                    }

                                    if (progressCounter > 0)
                                        Interlocked.Decrement(ref progressCounter);
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref totalFilesInspected);
                                Write($"No way: {dir}", LogLevel.Warning);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Write($"SemaphoreSearch: {ex.Message}", LogLevel.Warning);
                    }
                    finally
                    {
                        semaphore.Release();
                        isCompleted = true;
                        //_logQueue.Enqueue(new LogEntry { Message = $"Search task #{currId} is finished, tasks available: {semaphore.CurrentCount}", Severity = LogLevel.Verbose, Time = DateTime.Now });
                        Debug.WriteLine($"Search task #{currId} is finished, tasks available: {semaphore.CurrentCount}");
                    }
                }
                else // Current thread was not allowed to enter the semaphore, so keep waiting.
                {
                    //Debug.WriteLine($"Still waiting for task #{Task.CurrentId} to finish.");
                }

                // Extra precaution, but the Wait should properly observe the cts.
                if (cts.IsCancellationRequested)
                {
                    Write($"Cancellation requested for task #{Task.CurrentId}", LogLevel.Warning);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                //Debug.WriteLine($"Search canceled for task #{Task.CurrentId}");
                _logQueue.Enqueue(new LogEntry { Message = $"Search canceled for '{dir}'", Method = $"SemaphoreSearch", Severity = LogLevel.Warning, Time = DateTime.Now });
                break; //throw new OperationCanceledException();
            }
        }
    }

    /// <summary>
    /// Load any existing data for the folder and begin parsing.
    /// </summary>
    Dictionary<string, string> IndexFolder(string folderPath, string title, CancellationToken token, int maxThreads = 2)
    {
        Dictionary<string, string> results = new Dictionary<string, string>();

        try
        {
            var existing = MostRecent.Select(o => o).Where(m => m.Folder.Equals(folderPath, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (existing != null && existing.Count > 0)
            {
                progressCounter = existing.Count;
                Write($"Last count: {existing.Count}  Mode: {(maxThreads > 1 ? "multi-thread" : "single-thread")}", LogLevel.Info);
            }
            else
            {
                Write($"No previous count found, please wait while new one is created", LogLevel.Info);
                GetTotalCount(folderPath, ref progressCounter, token);
                lastCount = progressCounter;
                // Should we update the MRU if the counting was cancelled?
                UpdateMRU(folderPath, progressCounter);
            }

            // You don't want to go crazy with the thread count here.
            // In most cases throwing more threads at a problem does not
            // make it better, e.g. using 20 threads may cause the process
            // to finish slightly faster but will bring the client machine
            // to its knees; 2 to 4 threads is good for most repositories.
            if (maxThreads > 1)
            {
                var semaphore = new SemaphoreSlim(maxThreads, maxThreads);

                // This ignore list could be moved to the config for customizing.
                List<string> ignores = new();
                //ignores.Add(@"\.git");
                ignores.Add(@"\.vs");
                ignores.Add(@"\bin");
                ignores.Add(@"\obj");

                // If multi-threaded config, handle root folder first.
                IndexFiles(folderPath, ref results, token, true);

                // Now handle all subs.
                var dirs = Directory.GetDirectories(folderPath);
                if (dirs.Length > 0)
                {
                    Write($"Running {dirs.Length} tasks using {maxThreads} threads", LogLevel.Info);
                    int idx = 0;
                    Task[] semTasks = new Task[dirs.Length];
                    foreach (var dir in dirs)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        semTasks[idx++] = Task.Run(() =>
                        {
                            //var maxSearchTime = new CancellationTokenSource(new TimeSpan(0, 5, 0));
                            //SemaphoreSearch(dir, semaphore, maxSearchTime.Token, ref results);
                            SemaphoreSearch(dir, semaphore, token, ref results, ignores);
                        });
                        Thread.Sleep(10);
                    }
                    Write("Task queue filled", LogLevel.Info);

                    try
                    {
                        Task.WaitAll(semTasks);
                    }
                    catch (AggregateException aex)
                    {
                        aex.Flatten().Handle(ex =>
                        {
                            Write($"ExceptionType: {ex.GetType()}", LogLevel.Warning);
                            Write($"ExceptionMessage: {ex.Message}", LogLevel.Warning);
                            _lastError = ex.Message;
                            return true;
                        });
                    }
                }
            }
            else // use single thread for everything
            {
                IndexFiles(folderPath, ref results, token);
            }

            return results;
        }
        catch (Exception ex)
        {
            Write(ex.Message, LogLevel.Error);
            _lastError = ex.Message;
            return results;
        }
    }

    /// <summary>
    /// Single-threaded method to traverse folders and files.
    /// Will read the files in this folder and all subfolders.
    /// </summary>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <remarks>This is a recursive method.</remarks>
    void IndexFiles(string path, ref Dictionary<string, string> indexDB, CancellationToken token, bool rootOnly = false)
    {
        //string msg = string.Format("Indexing {0} ", path.Truncate(50, path.GetLast(50)));
        try
        {
            // We don't want common changes from the VS IDE being flagged as real file updates.
            if (path.Contains(@"\.git") || path.Contains(@"\.vs") || path.Contains(@"\bin") || path.Contains(@"\obj"))
            {
                // There's more than one file in these folders, but just
                // increment a few times for the progress estimation.
                totalFilesInspected += 50;
            }
            else
            {
                // Check for "path too long".
                if (Extensions.IsValidPath(path))
                {
                    foreach (string fi in Directory.GetFiles(path))
                    {
                        if (token.IsCancellationRequested)
                            break;
                        try
                        {
                            totalFilesInspected++;
                            if (Extensions.IsValidPath(fi))
                            {
                                // Assemble file information for hashing.
                                var name = Path.GetFileName(fi);
                                var info = new FileInfo(fi);
                                string toHash = $"{name}*{info.LastWriteTimeUtc}*{info.Directory}*{info.Length}";
                                var hashed = CRC32.Compute(toHash).ToString("X8");
                                indexDB.Add(fi, hashed);
                            }
                        }
                        catch (Exception ex)
                        {
                            // There is typically a path name involved with this error, and
                            // it may be very long, so just truncate the text before writing.
                            Write(ex.Message.Truncate(50, ex.Message.GetLast(50)), LogLevel.Warning);
                            _lastError = ex.Message;
                        }

                        if (progressCounter > 0) { progressCounter--; }
                    }
                }
                else
                {
                    totalFilesInspected++;
                    Write($"No way: '{path}'", LogLevel.Warning);
                }
            }

            if (!rootOnly)
            {
                foreach (string di in Directory.GetDirectories(path))
                {
                    if (token.IsCancellationRequested)
                        break;

                    // Recursive
                    IndexFiles(di, ref indexDB, token, rootOnly);
                }
            }
        }
        catch (Exception ex)
        {
            Write(ex.Message, LogLevel.Error);
            _lastError = ex.Message;
        }
    }

    /// <summary>
    /// Compression method for the resultant archive.
    /// </summary>
    /// <param name="name">name of zip file</param>
    /// <param name="location">folder containing files to zip</param>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <param name="update">whether to updated the archive or replace it</param>
    /// <remarks>
    /// We could multi-thread this, but it would need testing against the <see cref="System.IO.Compression.ZipFile"/>
    /// stream. The <see cref="System.IO.Compression.ZipArchiveMode.Update"/> mode would probably be the best approach.
    /// </remarks>
    bool ZipUpFiles(string name, string location, CancellationToken token, bool update = false)
    {
        string zipFile = Path.Combine(GetDefaultFolder(), name);

        try
        {
            string? partPath = "", partFile = "";

            if (!update && File.Exists(zipFile))
                File.Delete(zipFile);

            var files = Directory.GetFiles(location, "*.*", SearchOption.AllDirectories);
            if (File.Exists(zipFile))
            {
                using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Update))
                {
                    // Update all entries.
                    foreach (var fPath in files)
                    {
                        if (token.IsCancellationRequested)
                            return false;

                        if (!fPath.Equals(zipFile)) // don't zip self
                        {
                            partPath = Path.GetPathRoot(fPath) ?? "";
                            if (!string.IsNullOrEmpty(partPath))
                            {
                                // Remove root drive letter.
                                partFile = fPath.Replace(partPath, "");
                                // Check for existing
                                var test = archive.Entries.FirstOrDefault(o => o.FullName == partFile); //archive.Entries.Select(o => o).Where(o => o.FullName.Equals(partFile)).FirstOrDefault();
                                // Remove existing entry
                                if (test != null) { test.Delete(); }
                                // Add entry from file
                                var tmp = archive.CreateEntryFromFile(fPath, partFile);
                                Write($"Updated {tmp.Name} in archive {name}", LogLevel.Success);
                            }
                        }
                    }
                }
            }
            else // Create new archive.
            {
                using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
                {
                    foreach (var fPath in files)
                    {
                        if (token.IsCancellationRequested)
                            return false;

                        partPath = Path.GetPathRoot(fPath);
                        partFile = fPath.Replace(partPath ?? "NULL", ""); // Remove root drive letter.
                        var tmp = archive.CreateEntryFromFile(fPath, partFile);
                        Write($"Added {tmp.Name} to archive {name}", LogLevel.Success);
                    }
                }
            }

            // Auto-launch Windows file explorer?
            if (AppSettings.Config.ExplorerShell)
            {
                Write($"Showing archive", LogLevel.Info);
                Thread.Sleep(200);
                Process.Start($"explorer.exe", $"/select,{zipFile}");
            }
            else
            {
                Write($"Archive location: '{zipFile}'", LogLevel.Important);
            }

            return true;
        }
        catch (Exception ex)
        {
            Write($"ZipUpFiles: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Save indexed file dictionary to disk.
    /// TODO: Test UNC pathing.
    /// </summary>
    /// <returns>true if successful, false otherwise</returns>
    bool BackupFiles(string location, Dictionary<string, string> index, CancellationToken token)
    {
        // Reset the progress countdown for the copy (for large jobs).
        progressCounter = progressMax;

        try
        {
            string? partPath = "", partDir = "", partFile = "", partFinal = "";

            foreach (var kvp in index)
            {
                lock (lockObj) { progressCounter--; }

                if (token.IsCancellationRequested)
                    return false;

                // We want to recreate the folder structure up to but not including the root.
                partPath = System.IO.Path.GetPathRoot(kvp.Key);
                partFile = System.IO.Path.GetFileName(kvp.Key);
                partDir = System.IO.Path.GetDirectoryName(kvp.Key);

                if (location.EndsWith("\\"))
                    partFinal = System.IO.Path.GetDirectoryName(kvp.Key)?.Replace(partPath ?? "NULL", location);
                else
                    partFinal = System.IO.Path.GetDirectoryName(kvp.Key)?.Replace(partPath ?? "NULL", location + "\\");

                try
                {
                    if (!string.IsNullOrEmpty(partFinal))
                    {
                        if (!Directory.Exists(partFinal))
                            Directory.CreateDirectory(partFinal);

                        File.Copy(kvp.Key, System.IO.Path.Combine(partFinal, partFile), true);
                        //_logQueue.Enqueue(new LogEntry { Message = $"'{partFile}' copied to '{partFinal}'", Severity = LogLevel.Verbose, Time = DateTime.Now });
                        //Debug.WriteLine($"'{partFile}' copied to '{partFinal}'", LogLevel.Info);
                    } 
                }
                catch (Exception ex)
                {
                    Write($"{ex.Message}", LogLevel.Warning);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Compare the indexed file sets.
    /// </summary>
    Dictionary<string, string> CompareIndexSets(Dictionary<string, string> current, Dictionary<string, string> stored)
    {
        try
        {
            Dictionary<string, string> results = new Dictionary<string, string>();

            // Create a HashSet for Set1 dictionary
            HashSet<KeyValuePair<string, string>> hashSet1 = new HashSet<KeyValuePair<string, string>>(current);

            // Create a HashSet for Set2 dictionary
            HashSet<KeyValuePair<string, string>> hashSet2 = new HashSet<KeyValuePair<string, string>>(stored);

            // Creating a new HashSet that contains the exceptions
            HashSet<KeyValuePair<string, string>> excepts = new HashSet<KeyValuePair<string, string>>(hashSet1);
            excepts.ExceptWith(hashSet2);

            // Showing the unique elements of both HashSets
            if (excepts.Count() > 0)
            {
                Write($"Unique elements: {excepts.Count()}", LogLevel.Info);
                foreach (var kvp in excepts)
                    results.Add(kvp.Key, kvp.Value);
            }
            else
            {
                Write($"No modified elements were discovered", LogLevel.Info);
            }

            // This will be the list to create a new backup from.
            return results;
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Determine the age of our previously indexed set.
    /// </summary>
    Dictionary<string, string> CheckIndexFreshness(string indexTitle, string path, CancellationToken token, int days, bool updateIfOld = false)
    {
        try
        {
            string storeDB = Path.Combine(GetDefaultFolder(), indexTitle + ".idb");
            if (!File.Exists(storeDB))
                return new Dictionary<string, string>(); // return empty

            var info = new FileInfo(storeDB);
            if (info.LastWriteTime < DateTime.Now.Subtract(new TimeSpan(days, 0, 0, 0)))
            {
                // If the index is too old you may want to update
                // it instead of running a backup against it.
                if (updateIfOld)
                {
                    Write($"Updating {indexTitle} db", LogLevel.Info);
                    var result = IndexFolder(path, indexTitle, token, ThreadCount[AppSettings.Config.ThreadIndex]);
                    SaveIndexDB(indexTitle, result);
                    return result;
                }
                else
                {
                    Write($"Update declined, loading old index", LogLevel.Info);
                    return LoadIndexDB(indexTitle, days);
                }
            }
            else
            {
                Write($"{indexTitle} db is still fresh", LogLevel.Info);
                return LoadIndexDB(indexTitle, days);
            }
        }
        catch (Exception ex)
        {
            Write($"{ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Save an indexed file set to disk.
    /// </summary>
    bool SaveIndexDB(string indexTitle, Dictionary<string, string> index)
    {
        try
        {
            string storeDB = Path.Combine(GetDefaultFolder(), indexTitle + ".idb");
            Write($"Saving {indexTitle}…", LogLevel.Info);
            var result = WriteDictionary(storeDB, index, Encoding.Default, false);
            if (result)
            {
                Write($"Save successful: {storeDB}", LogLevel.Success);
                return true;
            }
            else
            {
                Write($"Save failed: {storeDB}", LogLevel.Warning);
                return false;
            }
        }
        catch (Exception ex)
        {
            Write($"SaveIndexDB: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Load an indexed file set from disk.
    /// </summary>
    Dictionary<string, string> LoadIndexDB(string indexTitle, int days)
    {
        try
        {
            string storeDB = Path.Combine(GetDefaultFolder(), indexTitle + ".idb");
            if (!File.Exists(storeDB))
                return new Dictionary<string, string>(); //return empty

            var info = new FileInfo(storeDB);
            if (info.LastWriteTime < DateTime.Now.Subtract(new TimeSpan(days, 0, 0, 0)))
                Write($"Index db is getting old, you should re-index soon", LogLevel.Info);

            Write($"Loading {indexTitle} ({info.LastWriteTime})", LogLevel.Info);

            var dict = ReadIntoDictionary(storeDB, Extensions.GetEncoding(storeDB));

            Write($"Loaded {dict?.Count} entries", LogLevel.Success);

            //foreach (KeyValuePair<string, string> kvp in dict)
            //    WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}", CurrentColor);

            return dict;
        }
        catch (Exception ex)
        {
            Write($"LoadIndexDB: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
            return new Dictionary<string, string>(); //return empty
        }
    }

    /// <summary>
    /// Load hash entries into working dictionary.
    /// Each entry will have two items:
    ///   1: The full file path and file name.
    ///   2: The hash of the file's properties.
    /// </summary>
    Dictionary<string, string> ReadIntoDictionary(string fileName, Encoding encoding, char separator = '*')
    {
        Dictionary<string, string> ds = new Dictionary<string, string>();
        try
        {
            using (StreamReader sr = new StreamReader(fileName, encoding))
            {
                while (sr.Peek() != -1)
                {
                    string? splitMe = sr.ReadLine();
                    if (!string.IsNullOrEmpty(splitMe))
                    {
                        string[] splits = splitMe.Split(separator);
                        if (splits.Length < 2)
                            continue;
                        else if (splits.Length == 2)
                            ds.Add(splits[0].Trim(), splits[1].Trim());
                        else if (splits.Length > 2)
                            Write($"Invalid entry: {splitMe}", LogLevel.Warning);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Write($"ReadIntoDictionary: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
        }
        return ds;
    }

    /// <summary>
    /// Save hash entries from working dictionary to file.
    /// Each entry will have two items:
    ///   1: The full file path and file name.
    ///   2: The hash of the file's properties.
    /// </summary>
    bool WriteDictionary(string fileName, Dictionary<string, string> fileData, Encoding encoding, bool append = true, char separator = '*')
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(fileName, append, encoding))
            {
                foreach (KeyValuePair<string, string> kvp in fileData)
                {
                    sw.WriteLine($"{kvp.Key}{separator}{kvp.Value}");
                    sw.Flush();
                }
                return true;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Write($"WriteDictionary: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
        }
        catch (Exception ex)
        {
            Write($"WriteDictionary: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
        }
        return false;
    }


    /// <summary>
    /// Helper method to keep zip bundles tidy.
    /// </summary>
    void RemovePreviousBackup(string path, CancellationToken token)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    Write($"Cleaning previous backup: {dir}", LogLevel.Info);
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Write($"RemovePreviousBackup: {ex.Message}", LogLevel.Error);
                    if (ex.Message.Contains("Access to the path"))
                        Write($"Ensure that read-only attributes are removed", LogLevel.Warning);
                    _logQueue.Enqueue(new LogEntry { Message = $"{ex.Message}", Method = "RemovePreviousBackup", Severity = LogLevel.Warning, Time = DateTime.Now });
                }
            }
        }
        catch (Exception ex)
        {
            Write($"RemovePreviousBackup: {ex.Message}", LogLevel.Error);
            _lastError = ex.Message;
        }
    }

    /// <summary>
    /// Update callback for progress.
    /// </summary>
    void TimerProcCallback(object? state)
    {
        if (progressCounter >= progressMax)
        {
            progressMax = progressCounter;
            Progress = $"Files: {progressCounter.ToString("#,###,##0")}";
        }
        else
        {
            float total = ((float)progressCounter / (float)progressMax) * 100;
            total = Math.Abs(100 - total);
            Progress = $"{total.ToString("0")}%";
            UpdateProgressBar((int)total);
        }

        // We could also monitor application thread count here:
        //int threadCount = Process.GetCurrentProcess().Threads.Count;
    }

    /// <summary>
    /// On an initial backup, we'll need to figure out how many 
    /// files we're dealing with so we can show the progress %.
    /// </summary>
    /// <remarks>This is a recursive method.</remarks>
    void GetTotalCount(string path, ref int count, CancellationToken token)
    {
        try
        {
            foreach (string fi in Directory.GetFiles(path))
            {
                if (token.IsCancellationRequested) { break; }
                count++;
            }

            foreach (string di in Directory.GetDirectories(path))
            {
                if (token.IsCancellationRequested) { break; }
                GetTotalCount(di, ref count, token);
            }
        }
        catch (Exception ex)
        {
            Write(ex.Message, LogLevel.Error);
            _lastError = ex.Message;
        }
    }

    /// <summary>
    /// Helper method
    /// </summary>
    /// <returns>the user's document folder path</returns>
    string GetDefaultFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return System.IO.Path.Combine(home, "Documents");
    }

    void Tracer(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Debug.WriteLine($"[{MethodBase.GetCurrentMethod()?.Name}]");
        Debug.WriteLine($"> Message....: " + message);
        Debug.WriteLine($"> Member name: " + memberName);
        Debug.WriteLine($"> File path..: " + sourceFilePath);
        Debug.WriteLine($"> Line number: " + sourceLineNumber);
    }

    /// <summary>
    /// Reset all properties related to a backup cycle.
    /// </summary>
    void ResetState()
    {
        _lastError = "";
        Progress = $"…";
        totalFilesInspected = 0;
        Messages.Clear();
        LogMessages.Clear();
        UpdateProgressBar(0);
    }
    #endregion

    #region [Toast Routines]
    /// <summary>
    /// Notify user using a <see cref="ToastNotification"/>.
    /// Supports up to two lines of text and embeds the application image.
    /// </summary>
    void ToastImageAndText(string text1, string text2 = "")
    {
        try
        {
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");

            stringElements.Item(0).AppendChild(toastXml.CreateTextNode(text1));
            if (!string.IsNullOrEmpty(text2))
                stringElements.Item(1).AppendChild(toastXml.CreateTextNode(text2));

            #region [Set image tag]
            var imgElement = toastXml.GetElementsByTagName("image");
            // Default <image> properties
            // ImageElement[x].Attribute[0] = id
            // ImageElement[x].Attribute[1] = src
            if (App.IsPackaged)
                imgElement[0].Attributes[1].NodeValue = $"ms-appdata:///local/Assets/RepoFolderTiny.png";
            else
                imgElement[0].Attributes[1].NodeValue = $"file:///{Directory.GetCurrentDirectory().Replace("\\", "/")}/Assets/RepoFolderTiny.png";
            /*
              [NOTES]
              - "ms-appx:///Assets/AppIcon.png" 
                    Does not seem to work (I've tried setting the asset to Content and Resource with no luck).
                    This may work with the AppNotificationBuilder, but Win10 does not support AppNotificationBuilder.
              - "file:///D:/AppIcon.png" 
                    Does work, however it is read at runtime so if the asset is missing it will only show the text notification.
              - "ms-appdata:///local/Assets/AppIcon.png"
                    Would be for packaged apps, I have not tested this.
              - "https://static.cdn.com/media/someImage.png"
                    I have not tested this extensively. Early tests did not work.
            */
            #endregion

            ToastNotification toast = new ToastNotification(toastXml);

            toast.Activated += ToastOnActivated;
            toast.Dismissed += ToastOnDismissed;
            toast.Failed += ToastOnFailed;

            // NOTE: It is critical that you provide the applicationID during CreateToastNotifier().
            // It is the name that will be used in the action center to group your toasts.
            var tnm = ToastNotificationManager.CreateToastNotifier("MenuDemo");
            if (tnm == null)
            {
                _logQueue.Enqueue(new LogEntry { Message = $"Could not create ToastNotificationManager.", Method = $"ToastImageAndText", Severity = LogLevel.Warning, Time = DateTime.Now });
                return;
            }

            var canShow = tnm.Setting;
            if (canShow != NotificationSetting.Enabled)
                _logQueue.Enqueue(new LogEntry { Message = $"Not allowed to show notifications because '{canShow}'.", Method = $"ToastImageAndText", Severity = LogLevel.Warning, Time = DateTime.Now });
            else
                tnm.Show(toast);

        }
        catch (Exception ex)
        {
            _logQueue.Enqueue(new LogEntry { Message = $"{ex.Message}", Method = $"ToastImageAndText", Severity = LogLevel.Error, Time = DateTime.Now });
        }
    }

    /// <summary>
    /// [Managing toast notifications in action center]
    /// https://learn.microsoft.com/en-us/previous-versions/windows/apps/dn631260(v=win.10)
    /// [The toast template catalog]
    /// https://learn.microsoft.com/en-us/previous-versions/windows/apps/hh761494(v=win.10)
    /// [Send a local toast notification from a C# app]
    /// https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/send-local-toast?tabs=desktop
    /// </summary>
    void ToastOnActivated(ToastNotification sender, object args)
    {
        // Handle notification activation
        if (args is Windows.UI.Notifications.ToastActivatedEventArgs toastActivationArgs)
        {
            // Obtain the arguments from the notification
            Debug.WriteLine(toastActivationArgs.Arguments);

            if (App.WindowsVersion.Major >= 10 && App.WindowsVersion.Build >= 18362)
            {
                // Obtain any user input (text boxes, menu selections) from the notification
                Windows.Foundation.Collections.ValueSet userInput = toastActivationArgs.UserInput;

                // Do something with the user selection.
                foreach (var item in userInput)
                {
                    string? key = item.Key;
                    object? value = item.Value;
                    Debug.WriteLine($"ToastKey: '{key}'  ToastValue: '{value}'");
                }
            }
        }
    }
    void ToastOnDismissed(ToastNotification sender, ToastDismissedEventArgs args)
    {
        _logQueue.Enqueue(new LogEntry { Message = $"Toast Dismissal Reason: {args.Reason}", Method = "ToastOnDismissed", Severity = LogLevel.Info, Time = DateTime.Now });
    }
    void ToastOnFailed(ToastNotification sender, ToastFailedEventArgs args)
    {
        _logQueue.Enqueue(new LogEntry { Message = $"Toast Failed: {args.ErrorCode.Message}", Method = "ToastOnFailed", Severity = LogLevel.Warning, Time = DateTime.Now });
    }

    /// <summary>
    /// Shows use of the ToastNotificationManager.History feature.
    /// </summary>
    void ShowToastHistory()
    {
        try
        {
            var notes = ToastNotificationManager.History.GetHistory("MenuDemo");
            foreach (var item in notes)
            {
                // Sample one of the fields...
                var et = item.ExpirationTime;
                Write($"Expires: {et}", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            _logQueue.Enqueue(new LogEntry { Message = $"{ex.Message}", Method = $"ShowToastHistory", Severity = LogLevel.Error, Time = DateTime.Now });
        }
    }
    #endregion

    #region [System Info]
    /// <summary>
    /// Returns the amount of CPU time this process has used.
    /// </summary>
    string GetCPUTime()
    {
        var time = App.GetCPUTime();
        return $"CPU time used: {time}";
    }

    /// <summary>
    /// Returns the amount of CPU time this process has used.
    /// </summary>
    string GetMemoryCommit()
    {
        var amnt = App.GetMemoryCommit();
        return $"Memory used: {amnt}";
    }

    /// <summary>
    /// Converts environment string into integer for compare ops.
    /// </summary>
    int GetProcessorCount()
    {
        try
        {
            return App.MachineAndUserVars["NUMBER_OF_PROCESSORS"] switch
            {
                "64" => 64,
                "32" => 32,
                "28" => 28,
                "24" => 24,
                "20" => 20,
                "18" => 18,
                "16" => 16,
                "14" => 14,
                "12" => 12,
                "10" => 10,
                "8" => 8,
                "6" => 6,
                "4" => 4,
                "2" => 2,
                "1" => 1,
                _ => 0
            };
        }
        catch (KeyNotFoundException) { return 2; }
    }

    const double GB = 1024 * 1024 * 1024;
    const double MBPS = 1000 * 1000;
    /// <summary>
    /// Gathers system information via our <see cref="NativeMethods"/> class.
    /// Will also utilize <see cref="Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation"/>
    /// and <see cref="Windows.System.Profile.AnalyticsInfo"/> classes.
    /// </summary>
    void UpdateSystemInfo()
    {
        try
        {
            #region [System Memory Load]
            NativeMethods.MEMORYSTATUSEX memoryStatus = new();
            NativeMethods.GlobalMemoryStatusEx(memoryStatus);
            var PhysicalMemory = $"total = {memoryStatus.ullTotalPhys / GB:N2}GB, available = {memoryStatus.ullAvailPhys / GB:N2}GB";
            var PhysicalPlusPagefile = $"total = {memoryStatus.ullTotalPageFile / GB:N2}GB, available = {memoryStatus.ullAvailPageFile / GB:N2}GB";
            var VirtualMemory = $"total = {memoryStatus.ullTotalVirtual / GB:N2}GB, available = {memoryStatus.ullAvailVirtual / GB:N2}GB";
            ulong pageFileOnDisk = memoryStatus.ullTotalPageFile - memoryStatus.ullTotalPhys;
            var PageFileOnDisk = $"{pageFileOnDisk / GB:N2}GB";
            var MemoryLoad = $"{memoryStatus.dwMemoryLoad}%";
            #endregion

            #region [Disk Space]
            ulong freeBytesAvailable;
            ulong totalNumberOfBytes;
            ulong totalNumberOfFreeBytes;
            NativeMethods.GetDiskFreeSpaceEx(Directory.GetCurrentDirectory(), out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes);
            var TotalDiskSize = $"{totalNumberOfBytes / GB:N2}GB";
            var DiskFreeSpace = $"{freeBytesAvailable / GB:N2}GB";
            DiskSpace = $"Total: {TotalDiskSize}    Free: {DiskFreeSpace}";
            #endregion

            #region [Machine Info]
            // EasClientDeviceInformation is also referenced in the App.xaml.cs public properties.
            Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation deviceInfo = new();
            var MachineName = deviceInfo.FriendlyName;        // "DEV-28"
            var OperatingSystem = deviceInfo.OperatingSystem; // "WINDOWS"
            var MakeModel = $"{deviceInfo.SystemManufacturer}, {deviceInfo.SystemProductName}";

            NativeMethods.SYSTEM_INFO sysInfo = new();
            NativeMethods.GetSystemInfo(ref sysInfo);
            var LogicalProcessors = sysInfo.dwNumberOfProcessors.ToString();
            var Processor = $"{sysInfo.wProcessorArchitecture}, level {sysInfo.wProcessorLevel}, rev {sysInfo.wProcessorRevision}";
            var PageSize = sysInfo.dwPageSize.ToString();

            // Determine Windows OS Version...
            string familyVersion = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong v = ulong.Parse(familyVersion);
            ulong v1 = (v & 0xFFFF000000000000L) >> 48; // Major
            ulong v2 = (v & 0x0000FFFF00000000L) >> 32; // Minor
            ulong v3 = (v & 0x00000000FFFF0000L) >> 16; // Build
            ulong v4 = (v & 0x000000000000FFFFL);       // Revision
            string OSVersion = $"{v1}.{v2}.{v3}.{v4}";
            Write($"{OperatingSystem} {OSVersion}", LogLevel.Info);
            #endregion

            #region [Network]
            IntPtr infoPtr = IntPtr.Zero;
            uint infoLen = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.FIXED_INFO>();
            int ret = -1;

            while (ret != NativeMethods.ERROR_SUCCESS)
            {
                infoPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(Convert.ToInt32(infoLen));
                ret = NativeMethods.GetNetworkParams(infoPtr, ref infoLen);
                if (ret == NativeMethods.ERROR_BUFFER_OVERFLOW)
                {
                    // Try again with a bigger buffer.
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(infoPtr);
                    continue;
                }
            }

            NativeMethods.FIXED_INFO info = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.FIXED_INFO>(infoPtr);
            var DomainName = info.DomainName;

            string nodeType = string.Empty;
            switch (info.NodeType)
            {
                case NativeMethods.BROADCAST_NODETYPE:
                    nodeType = "Broadcast";
                    break;
                case NativeMethods.PEER_TO_PEER_NODETYPE:
                    nodeType = "Peer to Peer";
                    break;
                case NativeMethods.MIXED_NODETYPE:
                    nodeType = "Mixed";
                    break;
                case NativeMethods.HYBRID_NODETYPE:
                    nodeType = "Hybrid";
                    break;
                default:
                    nodeType = $"Unknown ({info.NodeType})";
                    break;
            }
            var NodeType = nodeType;
            Debug.WriteLine($"NodeType: {NodeType}");

            Windows.Networking.Connectivity.ConnectionProfile profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            var ConnectedProfile = profile.ProfileName;
            Debug.WriteLine($"ProfileName: {ConnectedProfile}");

            Windows.Networking.Connectivity.NetworkAdapter internetAdapter = profile.NetworkAdapter;
            var IanaInterfaceType = $"{(IanaInterfaceType)internetAdapter.IanaInterfaceType}";
            var InboundSpeed = $"{internetAdapter.InboundMaxBitsPerSecond / MBPS:N0} Mbps";
            var OutboundSpeed = $"{internetAdapter.OutboundMaxBitsPerSecond / MBPS:N0} Mbps";

            IReadOnlyList<Windows.Networking.HostName> hostNames = Windows.Networking.Connectivity.NetworkInformation.GetHostNames();
            Windows.Networking.HostName? connectedHost = hostNames.Where
                (h => h.IPInformation != null
                && h.IPInformation.NetworkAdapter != null
                && h.IPInformation.NetworkAdapter.NetworkAdapterId == internetAdapter.NetworkAdapterId)
                .FirstOrDefault();
            if (connectedHost != null)
            {
                var HostAddress = connectedHost.CanonicalName;
                Debug.WriteLine($"HostAddress: {HostAddress}");
                var AddressType = connectedHost.Type.ToString();
                Debug.WriteLine($"AddressType: {AddressType}");
                Write($"{AddressType}: {HostAddress} ({ConnectedProfile} {NodeType})", LogLevel.Info);
            }
            #endregion

            #region [Battery/Power]
            bool isBatteryAvailable = true;
            try
            {
                NativeMethods.SYSTEM_POWER_STATUS powerStatus = new();
                NativeMethods.GetSystemPowerStatus(ref powerStatus);
                var ACLineStatus = powerStatus.ACLineStatus.ToString();

                var BatteryLife = "N/A";
                var BatteryChargeStatus = $"{powerStatus.BatteryChargeStatus:G}";
                if (powerStatus.BatteryChargeStatus == NativeMethods.BatteryFlag.NoSystemBattery
                    || powerStatus.BatteryChargeStatus == NativeMethods.BatteryFlag.Unknown)
                {
                    isBatteryAvailable = false;
                }
                else
                {
                    BatteryLife = $"{powerStatus.BatteryLifePercent}%";
                }
                var BatterySaver = powerStatus.BatterySaver.ToString();
                
                if (isBatteryAvailable)
                    Write($"Battery: {BatteryLife}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SYSTEM_POWER_STATUS: {ex.Message}");
            }

            string ChargeRate = "N/A";
            string Capacity = "N/A";
            if (isBatteryAvailable)
            {
                try
                {
                    Windows.Devices.Power.Battery battery = Windows.Devices.Power.Battery.AggregateBattery;
                    Windows.Devices.Power.BatteryReport batteryReport = battery.GetReport();
                    ChargeRate = $"{batteryReport.ChargeRateInMilliwatts:N0} mW";
                    Capacity = $"design = {batteryReport.DesignCapacityInMilliwattHours:N0} mWh, " +
                        $"full = {batteryReport.FullChargeCapacityInMilliwattHours:N0} mWh, " +
                        $"remaining = {batteryReport.RemainingCapacityInMilliwattHours:N0} mWh";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BatteryReport: {ex.Message}");
                }
            }
            #endregion
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateSystemInfo: {ex.Message}");
        }
    }
    #endregion

    #region [Miscellaneous]
    /// <summary>
    /// Determine internally referenced assemblies.
    /// This does not include loaded <see cref="ProcessModule"/>s.
    /// </summary>
    /// <returns>true if successful, false otherwise</returns>
    bool LogAssemblies()
    {
        try
        {
            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
                Thread.CurrentThread.Name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            Write($"{Thread.CurrentThread.Name} thread state: {Extensions.SimplifyState(Thread.CurrentThread.ThreadState)}", LogLevel.Debug);

            var items = Extensions.ListAllAssemblies();
            if (items.Count() > 0)
            {
                items.ToList().ForEach(item =>
                {
                    _logQueue.Enqueue(new LogEntry { Message = $"{item}", Method = "LogAssemblies", Severity = LogLevel.Debug, Time = DateTime.Now });
                });
            }
            else
                _logQueue.Enqueue(new LogEntry { Message = $"No other referenced assemblies detected", Method = "LogAssemblies", Severity = LogLevel.Debug, Time = DateTime.Now });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Test for our <see cref="PackageManagerHelper"/>.
    /// </summary>
    void LogPackages()
    {
        Task.Run(delegate ()
        {
            var packages = PackageManagerHelper.GatherPackages();
            foreach (var package in packages)
            {
                try
                {
                    string description = $"{package.DisplayName} [{package.InstalledLocation.Path}]";
                    Logger.WriteLine(description, LogLevel.Debug);
                }
                catch (Exception) { /* FileNotFound exceptions may occur for some installed locations. */ }
            }
        });
    }

    /// <summary>
    /// Walk through the application's theme dictionaries and collect all the color values.
    /// </summary>
    /// <returns><see cref="List{NamedColor}"/></returns>
    List<Models.NamedColor> GatherScopedColors()
    {
        List<Models.NamedColor> colorGroup = new();
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        if (dictionaries == null)
            return colorGroup;

        foreach (var item in dictionaries)
        {
            if (item.ThemeDictionaries.Count > 0)
            {
                Debug.WriteLine(item.ThemeDictionaries.ToString<object, object>());

                foreach (KeyValuePair<object, object> kvp in item.ThemeDictionaries)
                {
                    Debug.WriteLine($"Discovered '{kvp.Key}' theme");
                    var elements = kvp.Value as ResourceDictionary;
                    if (elements != null)
                    {
                        foreach (var element in elements.ToList())
                        {
                            var clrTest = element.Value.ToString();
                            if (!string.IsNullOrEmpty(clrTest) && clrTest.StartsWith("#"))
                            {
                                var tmpClr = Extensions.GetColorFromHexString(clrTest);
                                if (tmpClr != null)
                                    colorGroup.Add(new Models.NamedColor { KeyName = $"{element.Key}", HexCode = clrTest, Color = (Windows.UI.Color)tmpClr });
                            }
                        }
                    }
                }
            }
            else { Write($"No theme dictionaries found", LogLevel.Warning); }
        }
        return colorGroup;
    }

    /// <summary>
    /// Returns a random selection from <see cref="Microsoft.UI.Colors"/>.
    /// We are interested in the runtime <see cref="System.Reflection.PropertyInfo"/>
    /// from the <see cref="Microsoft.UI.Colors"/> sealed class. We will only add a
    /// property to our collection if it is of type <see cref="Windows.UI.Color"/>.
    /// </summary>
    /// <returns><see cref="List{T}"/></returns>
    public static List<Models.NamedColor>? GetWinUIColorList()
    {
        List<Models.NamedColor>? colors = new();
        StringBuilder hex = new StringBuilder();

        #region [Loop through the color properties of the Microsoft.UI.Color sealed class]
        foreach (var color in typeof(Microsoft.UI.Colors).GetRuntimeProperties())
        {
            // We must check the property type before assuming the explicit cast.
            if (color != null && color.PropertyType == typeof(Windows.UI.Color))
            {
                try
                {
                    var c = (Windows.UI.Color?)color.GetValue(null);
                    if (c != null)
                    {
                        hex.Clear();
                        hex.AppendFormat("{0:X2}", c?.A); // always FF
                        hex.AppendFormat("{0:X2}", c?.R);
                        hex.AppendFormat("{0:X2}", c?.G);
                        hex.AppendFormat("{0:X2}", c?.B);
                        colors.Add(new Models.NamedColor() 
                        { 
                            KeyName = $"{color.Name}", 
                            HexCode = $"{hex}", 
                            Color = c!= null ? (Windows.UI.Color)c : Windows.UI.Color.FromArgb(255,255,0,0) 
                        });
                    }
                }
                catch (Exception)
                {
                    Debug.WriteLine($"Failed to get the value for '{color.Name}'");
                }
            }
            else
            {
                Debug.WriteLine($"PropertyType => {color?.PropertyType}");
            }
        }
        #endregion

        #region [Loop through the public static fields of the Microsoft.UI.Color sealed class]
        //foreach (FieldInfo fieldInfo in typeof(Microsoft.UI.Colors).GetRuntimeFields())
        //{
        //    if (fieldInfo.IsPublic && fieldInfo.IsStatic)
        //    {
        //        Debug.WriteLine($"FieldName: {fieldInfo.Name}");
        //        if (fieldInfo.FieldType == typeof(Windows.UI.Color))
        //        {
        //            try { var color = (Windows.UI.Color)fieldInfo.GetValue(null); }
        //            catch (Exception ex) { Debug.WriteLine($"Failed to get the value for '{fieldInfo.Name}'"); }
        //        }
        //        else { Debug.WriteLine($"FieldType => {fieldInfo.FieldType}"); }
        //    }
        //}
        #endregion

        return colors;
    }

    /// <summary>
    /// This should be tied to an event which offers <see cref="KeyRoutedEventArgs"/>.
    /// </summary>
    async void TestNoticeDialog(object sender, KeyRoutedEventArgs e)
    {
        // Setup the dialog.
        var dialog = new SaveCloseDiscardDialog(
         saveAndExitAction: async () =>
         {
             Write($"Save and exit action", LogLevel.Debug);
             await Task.Delay(250);
         },
         discardAndExitAction: () =>
         {
             Write($"Discard and exit action", LogLevel.Debug);
         },
         cancelAction: () =>
         {
             Write($"Cancel action", LogLevel.Debug);
             e.Handled = true;
         },
         content: "Would you like to save your changes?");

        // Show the dialog.
        var result = await DialogManager.OpenDialogAsync(dialog, awaitPreviousDialog: false);

        // Deal with the result.
        if (result == null)
        {
            Write($"Result is null.", LogLevel.Debug);
            e.Handled = true;
        }
        else if (result == ContentDialogResult.Primary)
        {
            Write($"You chose 'Save'", LogLevel.Debug);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            Write($"You chose 'Discard'", LogLevel.Debug);
        }
        else if (result == ContentDialogResult.None)
        {
            Write($"You chose 'Close'", LogLevel.Debug);
        }

        if (e.Handled && !dialog.IsAborted)
            tbPath.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// This should be tied to an event which offers <see cref="KeyRoutedEventArgs"/>.
    /// </summary>
    async void TestGenericDialog(object sender, KeyRoutedEventArgs e)
    {
        string primaryText = "Yes";
        string secondaryText = "No";
        string cancelText = "Cancel";

        // Setup the dialog.
        var dialog = new GenericDialog(
         primaryText, yesAction: () =>
         {
             Write($"Running '{primaryText}' action", LogLevel.Debug);
         },
         secondaryText, noAction: () =>
         {
             Write($"Running '{secondaryText}' action", LogLevel.Debug);
         },
         cancelText, cancelAction: () =>
         {
             Write($"Running '{cancelText}' action", LogLevel.Debug);
             e.Handled = true;
         },
         title: "Question", content: "Are you sure?");

        // Show the dialog.
        var result = await DialogManager.OpenDialogAsync(dialog, awaitPreviousDialog: false);

        // Deal with the result.
        if (result == null)
        {
            Write($"Result is null.", LogLevel.Debug);
            e.Handled = true;
        }
        else if (result == ContentDialogResult.Primary)
        {
            Write($"You chose '{primaryText}'", LogLevel.Debug);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            Write($"You chose '{secondaryText}'", LogLevel.Debug);
        }
        else if (result == ContentDialogResult.None)
        {
            Write($"You chose '{cancelText}'", LogLevel.Debug);
        }

        if (e.Handled && !dialog.IsAborted)
            tbPath.Focus(FocusState.Programmatic);
    }
    #endregion

    #region [Custom Input Control Events]
    void TextInputOnDismissKeyDown(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[TextInputOnDismissKeyDown]");
        InputStoryboardHide.Begin();
    }

    void TextInputOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        Debug.WriteLine($"[TextInputOnKeyDown] => {e.Key}");
    }

    void TextInputOnButtonClicked(object sender, Controls.TextInputEventArgs e)
    {
        bool match = false;

        // We will receive our custom event args (TextInputEventArgs) once the button is clicked.
        Debug.WriteLine($"[TextInputOnButtonClicked] => {e.Data}");

        if (!string.IsNullOrEmpty(e.Data))
        {
            switch (e.Data)
            {
                case string s when s.StartsWith("device"):
                    match = true;
                    Write($"Device interface count: {_dwh?.DeviceInterfacesOutputList.Count}", LogLevel.Info);
                    //Logger?.WriteLinesAsync(_dwh.DeviceInterfacesOutputList, LogLevel.Debug);
                    break;
                case string s when s.StartsWith("log-device"):
                    match = true;
                    Write($"Saving {_dwh?.DeviceInterfacesOutputList.Count} devices", LogLevel.Info);
                    Logger?.WriteLinesAsync(_dwh.DeviceInterfacesOutputList, LogLevel.Debug);
                    break;
                case string s when s.StartsWith("config"):
                    match = true;
                    Write($"{AppSettings.Config.ToJsonObject()}", LogLevel.Info);
                    break;
                case string s when s.StartsWith("total"):
                    Write($"{InspectionCount}", LogLevel.Info);
                    break;
                case string s when s.StartsWith("count"):
                    match = true;
                    Write($"{totalFilesInspected}", LogLevel.Info);
                    break;
                default:
                    match = false;
                    Write($"Input did not match any known command", LogLevel.Warning);
                    break;
            }
        }

        // We can get the control using a direct approach...
        TextInputControl? tc = (TextInputControl?)spInputControl.FindName("inputControl");

        // Or we can iterate over each child until we find what we want...
        foreach (var element in spInputControl.GetChildren())
        {
            if (element is TextInputControl tic)
            {
                var controlHeight = tic.GetHeight();
                //Write($"Control height is {controlHeight}", LogLevel.Debug);

                if (match)
                {
                    tic.ClearInputData();
                    tic.Focus();
                }
                else
                {
                    tic.SetInputData(e.Data, 50, false);
                    tic.Focus(true);
                }

                break; // We're interested in only one, but you could have multiple.
            }
        }
    }

    /// <summary>
    /// Animation event (shown)
    /// </summary>
    void InputStoryboardShowOnCompleted(object? sender, object e)
    {
        inputControl.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Animation event (hidden)
    /// </summary>
    void InputStoryboardHideOnCompleted(object? sender, object e)
    {
        spInputControl.Visibility = Visibility.Collapsed;
    }
    #endregion
}

/// <summary>
/// Support class for method invoking directly from the XAML code in FileBackupView.xaml
/// This could be done using converters, but I like to show different techniques offering the same result.
/// </summary>
public static class AssemblyHelper
{
    /// <summary>
    /// Return the declaring type's version.
    /// </summary>
    /// <remarks>Includes string formatting.</remarks>
    public static string GetVersion()
    {
        var ver = App.GetCurrentAssemblyVersion();
        return $"Version {ver}";
    }

    /// <summary>
    /// Return the declaring type's namespace.
    /// </summary>
    public static string? GetNamespace()
    {
        var assembly = App.GetCurrentNamespace();
        return assembly ?? "Unknown";
    }

    /// <summary>
    /// Return the declaring type's assembly name.
    /// </summary>
    public static string? GetAssemblyName()
    {
        var assembly = App.GetCurrentAssemblyName()?.Split(',')[0].SeparateCamelCase();
        return assembly ?? "Unknown";
    }
}
