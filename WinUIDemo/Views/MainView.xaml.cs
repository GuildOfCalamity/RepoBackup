#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Search;
using WinUIDemo.Models;

namespace WinUIDemo.Views;

public sealed partial class MainView : UserControl
{
    public ViewModels.MainViewModel? ViewModel { get; private set; }
    
    public MainView()
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name}");

        ViewModel = App.GetService<ViewModels.MainViewModel>();
        this.InitializeComponent();

		this.Loaded += MainViewOnLoaded;
	}

    void MainViewOnLoaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name}");
        
        if (ViewModel != null && ViewModel.ShowFrame)
        {
            frame.Content = new Frame();
            frame.Opacity = 1d;
            frame.Navigate(typeof(TestPage));
        }
    }
}
