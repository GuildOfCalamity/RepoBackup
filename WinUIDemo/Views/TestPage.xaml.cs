using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Printing;

using WinUIDemo.Printing;

namespace WinUIDemo.Views;

/// <summary>
/// https://github.com/marb2000/PrintSample
/// Still throws "Value does not fall within expected range" exception.
/// https://github.com/microsoft/microsoft-ui-xaml/issues/4419
/// </summary>
public sealed partial class TestPage : Page
{
    public TestPage()
    {
        this.InitializeComponent();

        // Register for printing
        if (PrintManager.IsSupported())
        {
            PrintArgs.RegisterForPrinting(this);
        }
    }

    async void OnPrintClick(object sender, RoutedEventArgs e)
    {
        await Print("Testing the print engine in WinUI3.");
    }

    public async Task Print(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        
        await PrintAll(new[] { text });
    }

    public async Task PrintAll(string[] pages)
    {
        if (pages == null || pages.Length == 0)
            return;

        // Initialize print content
        PrintArgs.PreparePrintContent(pages);

        if (PrintManager.IsSupported())
        {
            // Show print UI
            await PrintArgs.ShowPrintUIAsync();
        }
        else if (!PrintManager.IsSupported())
        {
            await App.MainRoot?.MessageDialogAsync("Warning", $"Printing is not supported on this device");
        }
    }

}
