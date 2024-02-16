using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Printing;
using Microsoft.UI.Xaml;

using Windows.Graphics.Printing.OptionDetails;
using Windows.Graphics.Printing;
using System.Diagnostics;

namespace WinUIDemo.Printing;

/// <summary>
/// Still throws "Value does not fall within expected range" exception.
/// https://github.com/microsoft/microsoft-ui-xaml/issues/4419
///
/// Some code borrowed from https://github.com/microsoft/Windows-universal-samples/blob/f7bec1640bc2b773f02f102d13c12c04833a6c0e/Samples/Printing/cs/PrintHelper.cs
/// </summary>
public static class PrintArgs
{
    /// <summary>
    /// The text that appears at the top of every page
    /// </summary>
    private static string _headerText = string.Empty;

    /// <summary>
    /// The text that appears at the bottom of every page
    /// </summary>
    private static string _footerText = string.Empty;

    /// <summary>
    /// The requested font name
    /// </summary>
    private static string _fontName = "Consolas";

    /// <summary>
    /// The requested font size
    /// </summary>
    private static double _fontSize = 12d;

    /// <summary>
    /// The percent of app's margin width, content is set at 85% (0.85) of the area's width
    /// </summary>
    private static double _applicationContentMarginLeft = 0.075;

    /// <summary>
    /// The percent of app's margin height, content is set at 94% (0.94) of tha area's height
    /// </summary>
    private static double _applicationContentMarginTop = 0.03;

    /// <summary>
    /// PrintDocument is used to prepare the pages for printing.
    /// Prepare the pages to print in the handlers for the Paginate, GetPreviewPage, and AddPages events.
    /// </summary>
    private static Microsoft.UI.Xaml.Printing.PrintDocument _printDocument;

    /// <summary>
    /// Marker interface for document source
    /// </summary>
    private static IPrintDocumentSource _printDocumentSource;

    /// <summary>
    /// A list of UIElements used to store the print preview pages.  This gives easy access
    /// to any desired preview page.
    /// </summary>
    private static List<UIElement> _printPreviewPages;

    /// <summary>
    /// First page in the printing-content series
    /// From this "virtual sized" paged content is split(text is flowing) to "printing pages"
    /// </summary>
    private static List<FrameworkElement> _firstPage;

    /// <summary>
    ///  A reference back to the source page used to access XAML elements on the source page
    /// </summary>
    private static Page _sourcePage;

    /// <summary>
    ///  A hidden canvas used to hold pages we wish to print
    /// </summary>
    private static Canvas PrintCanvas => _sourcePage.FindName("PrintCanvas") as Canvas;

    /// <summary>
    /// Method that will generate print content for the scenario
    /// It will create the first page from which content will flow
    /// </summary>
    /// <param name="textEditors">Array of Notepads ITextEditors to print</param>
    public static void PreparePrintContent(string[] pages, string fontName= "Consolas", double fontSize = 12d)
    {
        _fontName = fontName;
        _fontSize = fontSize;

        // Clear the cache of preview pages
        _printPreviewPages.Clear();

        // Clear cache of first pages of each editor
        _firstPage.Clear();

        if (PrintCanvas == null)
            throw new Exception($"{nameof(PrintCanvas)} cannot be null");

        // Clear the print canvas of preview pages
        PrintCanvas.Children.Clear();

        foreach (var text in pages)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var page = new PrintFormat(
                    text,
                    new FontFamily(_fontName),
                    _fontSize,
                    _headerText,
                    _footerText);

                _firstPage.Add(page);

                // Add the (newly created) page to the print canvas which is part of the visual tree and force it to
                // go through layout so that the linked containers correctly distribute the content inside them.
                PrintCanvas.Children.Add(page);
                PrintCanvas.InvalidateMeasure();
                PrintCanvas.UpdateLayout();
            }
        }
    }

    /// <summary>
    /// This function registers the app for printing with Windows and sets up the necessary event handlers for the print process.
    /// </summary>
    public static void RegisterForPrinting(Page sourcePage)
    {
        _sourcePage = sourcePage;
        _printPreviewPages = new List<UIElement>();
        _firstPage = new List<FrameworkElement>();

        _printDocument = new PrintDocument();
        _printDocumentSource = _printDocument.DocumentSource;
        _printDocument.Paginate += CreatePrintPreviewPages;
        _printDocument.GetPreviewPage += GetPrintPreviewPage;
        _printDocument.AddPages += AddPrintPages;

        // https://github.com/microsoft/microsoft-ui-xaml/issues/4419
        // Generally, GetForCurrentView() methods don't work in WinUI 3 desktop apps, which don't have a core window.
        // Instead, there is a COM interop you can use: IPrintManagerInterop
        // https://docs.microsoft.com/en-us/windows/win32/api/printmanagerinterop/nn-printmanagerinterop-iprintmanagerinterop
        //PrintManager printMan = PrintManager.GetForCurrentView();
        //var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        PrintManager printMan = PrintManagerInterop.GetForWindow(App.WindowHandle);
        printMan.PrintTaskRequested += PrintTaskRequested;

        // Show printer dialog.
        //await PrintManagerInterop.ShowPrintUIForWindowAsync(hWnd);
    }

    /// <summary>
    /// This function un-registers the app for printing with Windows.
    /// </summary>
    public static void UnregisterForPrinting()
    {
        if (_printDocument == null)
        {
            return;
        }

        _printDocument.Paginate -= CreatePrintPreviewPages;
        _printDocument.GetPreviewPage -= GetPrintPreviewPage;
        _printDocument.AddPages -= AddPrintPages;

        // https://github.com/microsoft/microsoft-ui-xaml/issues/4419
        // Generally, GetForCurrentView() methods don't work in WinUI 3 desktop apps, which don't have a core window.
        // Instead, there is a COM interop you can use: IPrintManagerInterop
        // https://docs.microsoft.com/en-us/windows/win32/api/printmanagerinterop/nn-printmanagerinterop-iprintmanagerinterop
        //PrintManager printMan = PrintManager.GetForCurrentView();
        PrintManager printMan = PrintManagerInterop.GetForWindow(App.WindowHandle);

        // Remove the handler for printing initialization.
        printMan.PrintTaskRequested -= PrintTaskRequested;

        if (PrintCanvas != null)
            PrintCanvas.Children.Clear();
    }

    /// <summary>
    /// This is the event handler for PrintDocument.Paginate. It creates print preview pages for the app.
    /// </summary>
    /// <param name="sender">PrintDocument</param>
    /// <param name="e">Paginate Event Arguments</param>
    private static void CreatePrintPreviewPages(object sender, Microsoft.UI.Xaml.Printing.PaginateEventArgs e)
    {
        lock (_printPreviewPages)
        {
            // Clear the cache of preview pages
            _printPreviewPages.Clear();

            // Clear the print canvas of preview pages
            PrintCanvas.Children.Clear();

            // This variable keeps track of the last RichTextBlockOverflow element that was added to a page which will be printed
            RichTextBlockOverflow lastRTBOOnPage;

            // Get the PrintTaskOptions
            PrintTaskOptions printingOptions = e.PrintTaskOptions;

            // Get the page description to determine how big the page is
            PrintPageDescription pageDescription = printingOptions.GetPageDescription(0);

            var count = 0;
            do
            {
                // We know there is at least one page to be printed. passing null as the first parameter to
                // AddOnePrintPreviewPage tells the function to add the first page.
                lastRTBOOnPage = AddOnePrintPreviewPage(null, pageDescription, count);

                // We know there are more pages to be added as long as the last RichTextBoxOverflow added to a print preview
                // page has extra content
                while (lastRTBOOnPage.HasOverflowContent && lastRTBOOnPage.Visibility == Visibility.Visible)
                {
                    lastRTBOOnPage = AddOnePrintPreviewPage(lastRTBOOnPage, pageDescription, count);
                }

                count += 1;
            } while (count < _firstPage.Count);

            Microsoft.UI.Xaml.Printing.PrintDocument printDoc = (Microsoft.UI.Xaml.Printing.PrintDocument)sender;

            // Report the number of preview pages created
            printDoc.SetPreviewPageCount(_printPreviewPages.Count, PreviewPageCountType.Intermediate);
        }
    }

    /// <summary>
    /// This function creates and adds one print preview page to the internal cache of print preview
    /// pages stored in _printPreviewPages.
    /// </summary>
    /// <param name="lastRTBOAdded">Last RichTextBlockOverflow element added in the current content</param>
    /// <param name="printPageDescription">Printer's page description</param>
    static RichTextBlockOverflow AddOnePrintPreviewPage(RichTextBlockOverflow lastRTBOAdded, PrintPageDescription printPageDescription, int count)
    {
        // XAML element that is used to represent to "printing page"
        FrameworkElement page;

        // The link container for text overflowing in this page
        RichTextBlockOverflow textLink;

        // Check if this is the first page (no previous RichTextBlockOverflow)
        if (lastRTBOAdded == null)
        {
            // If this is the first page add the specific scenario content
            page = _firstPage[count];

            // Hide header and footer if not provided
            StackPanel header = (StackPanel)page.FindName("Header");
            if (!string.IsNullOrEmpty(_headerText))
            {
                header.Visibility = Visibility.Visible;
                TextBlock headerTextBlock = (TextBlock)page.FindName("HeaderTextBlock");
                headerTextBlock.Text = _headerText;
            }
            else
            {
                header.Visibility = Visibility.Collapsed;
            }

            StackPanel footer = (StackPanel)page.FindName("Footer");
            if (!string.IsNullOrEmpty(_footerText))
            {
                footer.Visibility = Visibility.Visible;
                TextBlock footerTextBlock = (TextBlock)page.FindName("FooterTextBlock");
                footerTextBlock.Text = _footerText;
            }
            else
            {
                footer.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            // Flow content/text from previous pages
            page = new ContinuationFormat(
                lastRTBOAdded,
                new FontFamily(_fontName),
                _fontSize,
                _headerText,
                _footerText);
        }

        // Set paper width
        page.Width = printPageDescription.PageSize.Width;
        page.Height = printPageDescription.PageSize.Height;

        Grid printableArea = (Grid)page.FindName("PrintableArea");

        // Get the margins size
        // If the ImageableRect is smaller than the app provided margins use the ImageableRect
        double marginWidth = Math.Max(printPageDescription.PageSize.Width - printPageDescription.ImageableRect.Width, printPageDescription.PageSize.Width * _applicationContentMarginLeft * 2);
        double marginHeight = Math.Max(printPageDescription.PageSize.Height - printPageDescription.ImageableRect.Height, printPageDescription.PageSize.Height * _applicationContentMarginTop * 2);

        // Set-up "printable area" on the "paper"
        printableArea.Width = _firstPage[count].Width - marginWidth;
        printableArea.Height = _firstPage[count].Height - marginHeight;

        if (PrintCanvas == null)
            throw new Exception($"{nameof(PrintCanvas)} cannot be null");

        // Add the (newly created) page to the print canvas which is part of the visual tree and force it
        // to go through layout so that the linked containers correctly distribute the content inside them.
        PrintCanvas.Children.Add(page);
        PrintCanvas.InvalidateMeasure();
        PrintCanvas.UpdateLayout();

        // Find the last text container and see if the content is overflowing
        textLink = (RichTextBlockOverflow)page.FindName("ContinuationPageLinkedContainer");

        // Check if this is the last page
        if (!textLink.HasOverflowContent && textLink.Visibility == Visibility.Visible)
        {
            PrintCanvas.UpdateLayout();
        }

        // Add the page to the page preview collection
        _printPreviewPages.Add(page);

        return textLink;
    }

    /// <summary>
    /// This is the event handler for PrintDocument.GetPrintPreviewPage. It provides a specific print 
    /// preview page, in the form of an UIElement, to an instance of PrintDocument. PrintDocument 
    /// subsequently converts the UIElement into a page that the Windows print system can deal with.
    /// </summary>
    /// <param name="sender">PrintDocument</param>
    /// <param name="e">Arguments containing the preview requested page</param>
    private static void GetPrintPreviewPage(object sender, Microsoft.UI.Xaml.Printing.GetPreviewPageEventArgs e)
    {
        Microsoft.UI.Xaml.Printing.PrintDocument printDoc = (Microsoft.UI.Xaml.Printing.PrintDocument)sender;
        printDoc.SetPreviewPage(e.PageNumber, _printPreviewPages[e.PageNumber - 1]);
    }

    /// <summary>
    /// This is the event handler for PrintDocument.AddPages. It provides all pages to be printed, in the form of
    /// UIElements, to an instance of PrintDocument. PrintDocument subsequently converts the UIElements
    /// into a pages that the Windows print system can deal with.
    /// </summary>
    /// <param name="sender">PrintDocument</param>
    /// <param name="e">Add page event arguments containing a print task options reference</param>
    private static void AddPrintPages(object sender, Microsoft.UI.Xaml.Printing.AddPagesEventArgs e)
    {
        // Loop over all of the preview pages and add each one to  add each page to be printied
        for (int i = 0; i < _printPreviewPages.Count; i++)
        {
            // We should have all pages ready at this point...
            _printDocument.AddPage(_printPreviewPages[i]);
        }

        Microsoft.UI.Xaml.Printing.PrintDocument printDoc = (Microsoft.UI.Xaml.Printing.PrintDocument)sender;

        // Indicate that all of the print pages have been provided
        printDoc.AddPagesComplete();
    }

	/// <summary>
	/// This is the event handler for PrintManager.PrintTaskRequested.
	/// We are getting stuck here, there must be more info required by the Request.CreatePrintTask()?
	/// </summary>
	/// <param name="sender">PrintManager</param>
	/// <param name="e">PrintTaskRequestedEventArgs </param>
	private static void PrintTaskRequested(
        Windows.Graphics.Printing.PrintManager sender, 
        Windows.Graphics.Printing.PrintTaskRequestedEventArgs e)
    {
        try
        {
            Windows.Graphics.Printing.PrintTask printTask = null;
            printTask = e.Request.CreatePrintTask(App.AppName, sourceRequestedArgs =>
            {
                var deferral = sourceRequestedArgs.GetDeferral();
                Windows.Graphics.Printing.OptionDetails.PrintTaskOptionDetails printDetailedOptions = Windows.Graphics.Printing.OptionDetails.PrintTaskOptionDetails.GetFromPrintTaskOptions(printTask.Options);
                IList<string> displayedOptions = printTask.Options.DisplayedOptions;

                // Choose the printer options to be shown.
                // The order in which the options are appended determines the order in which they appear in the UI
                displayedOptions.Clear();
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.Orientation);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.Copies);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.MediaSize);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.InputBin);

                // Add Margin setting in % options
                Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails leftMargin = printDetailedOptions.CreateTextOption("LeftMargin", "Print_LeftMarginEntry_Title");
                Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails topMargin = printDetailedOptions.CreateTextOption("TopMargin", "Print_TopMarginEntry_Title");
                leftMargin.Description = topMargin.Description = "Print_MarginEntry_Description";
                leftMargin.TrySetValue(Math.Round(100 * _applicationContentMarginLeft, 1).ToString());
                topMargin.TrySetValue(Math.Round(100 * _applicationContentMarginTop, 1).ToString());
                displayedOptions.Add("LeftMargin");
                displayedOptions.Add("TopMargin");

                // Add Header and Footer text options
                Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails headerText = printDetailedOptions.CreateTextOption("HeaderText", "Print_HeaderEntry_Title");
                Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails footerText = printDetailedOptions.CreateTextOption("FooterText", "Print_FooterEntry_Title");
                headerText.TrySetValue(_headerText);
                footerText.TrySetValue(_footerText);
                displayedOptions.Add("HeaderText");
                displayedOptions.Add("FooterText");

                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.CustomPageRanges);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.Duplex);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.Collation);
                //displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.NUp);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.MediaType);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.Bordering);
                //displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.ColorMode);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.PrintQuality);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.HolePunch);
                displayedOptions.Add(Windows.Graphics.Printing.StandardPrintTaskOptions.Staple);

                // Preset the default value of the printer option
                printTask.Options.MediaSize = PrintMediaSize.Default;

                printDetailedOptions.OptionChanged += PrintDetailedOptions_OptionChanged;

                // Print Task event handler is invoked when the print job is completed.
                printTask.Completed += async (s, args) =>
                {
                    // Notify the user when the print operation fails.
                    if (args.Completion == PrintTaskCompletion.Failed)
                    {
                        await App.MainRoot?.MessageDialogAsync("Warning", "Print task failed");
                    }
                };

                sourceRequestedArgs.SetSource(_printDocumentSource);

                deferral.Complete();
            });
        }
        catch (Exception ex)
        {
			Debug.WriteLine($"{ex.Message} ({ex.HResult})", nameof(PrintArgs));
		}
	}

    /// <summary>
    /// This is the event handler for PrintManager option changed.
    /// </summary>
    /// <param name="sender">PrintTaskOptionDetails</param>
    /// <param name="args">PrintTaskOptionChangedEventArgs </param>
    static async void PrintDetailedOptions_OptionChanged(
        Windows.Graphics.Printing.OptionDetails.PrintTaskOptionDetails sender, 
        Windows.Graphics.Printing.OptionDetails.PrintTaskOptionChangedEventArgs args)
    {
        bool invalidatePreview = false;

        string optionId = args.OptionId as string;
        if (string.IsNullOrEmpty(optionId))
        {
            return;
        }

        if (optionId == "HeaderText")
        {
            Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails headerText = (Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails)sender.Options["HeaderText"];
            _headerText = headerText.Value.ToString();
            invalidatePreview = true;
        }

        if (optionId == "FooterText")
        {
            Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails footerText = (Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails)sender.Options["FooterText"];
            _footerText = footerText.Value.ToString();
            invalidatePreview = true;
        }

        if (optionId == "LeftMargin")
        {
            Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails leftMargin = (Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails)sender.Options["LeftMargin"];
            var leftMarginValueConverterArg = double.TryParse(leftMargin.Value.ToString(), out var leftMarginValue);
            if (leftMarginValue > 50 || leftMarginValue < 0 || !leftMarginValueConverterArg)
            {
                leftMargin.ErrorText = "Print_ErrorMsg_ValueOutOfRange";
                return;
            }
            else if (Math.Round(leftMarginValue, 1) != leftMarginValue)
            {
                leftMargin.ErrorText = "Print_ErrorMsg_DecimalOutOfRange";
                return;
            }
            leftMargin.ErrorText = string.Empty;
            _applicationContentMarginLeft = Math.Round(leftMarginValue / 100, 3);
            invalidatePreview = true;
        }

        if (optionId == "TopMargin")
        {
            Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails topMargin = (Windows.Graphics.Printing.OptionDetails.PrintCustomTextOptionDetails)sender.Options["TopMargin"];
            var topMarginValueConverterArg = double.TryParse(topMargin.Value.ToString(), out var topMarginValue);
            if (topMarginValue > 50 || topMarginValue < 0 || !topMarginValueConverterArg)
            {
                topMargin.ErrorText = "Print_ErrorMsg_ValueOutOfRange";
                return;
            }
            else if (Math.Round(topMarginValue, 1) != topMarginValue)
            {
                topMargin.ErrorText = "Print_ErrorMsg_DecimalOutOfRange";
                return;
            }
            topMargin.ErrorText = string.Empty;
            _applicationContentMarginTop = Math.Round(topMarginValue / 100, 3);
            invalidatePreview = true;
        }

        if (invalidatePreview)
        {
            var dis = DispatcherQueue.GetForCurrentThread();
            dis.TryEnqueue(delegate ()
            {
                _printDocument.InvalidatePreview(); // needs to be done on a UI thread?
            });
        }
    }

    /// <summary>
    /// This method will show PrintManager UI and its options
    /// Any printing error will be handled by this method
    /// </summary>
    public static async Task ShowPrintUIAsync()
    {
        // Catch and print out any errors reported
        try
        {
            //await PrintManager.ShowPrintUIAsync();
            await PrintManagerInterop.ShowPrintUIForWindowAsync(App.WindowHandle);
        }
        catch (Exception ex)
        {
            await App.MainRoot?.MessageDialogAsync("PrintError", $"{ex.Message} ({ex.HResult})");
        }
    }
}
