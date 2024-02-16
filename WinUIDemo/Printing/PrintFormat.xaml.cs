using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace WinUIDemo.Printing;

/// <summary>
/// Formatting page used as a carrier for the print function.
/// </summary>
public sealed partial class PrintFormat : Page
{
    public RichTextBlock TextContentBlock { get; set; }

    public PrintFormat(string textEditorText, FontFamily textEditorFontFamily, double textEditorFontSize, string headerText, string footerText)
    {
        InitializeComponent();

        TextContent.FontFamily = textEditorFontFamily;
        TextContent.FontSize = textEditorFontSize;

        if (!string.IsNullOrEmpty(headerText))
        {
            Header.Visibility = Visibility.Visible;
            Header.Margin = new Thickness(0, 0, 0, textEditorFontSize + 6);
            HeaderTextBlock.Text = headerText;
            HeaderTextBlock.FontFamily = textEditorFontFamily;
            HeaderTextBlock.FontSize = textEditorFontSize + 4;
        }
        else
        {
            Header.Visibility = Visibility.Collapsed;
            HeaderTextBlock.FontFamily = textEditorFontFamily;
            HeaderTextBlock.FontSize = textEditorFontSize + 4;
        }

        if (!string.IsNullOrEmpty(footerText))
        {
            Footer.Visibility = Visibility.Visible;
            FooterTextBlock.Text = footerText;
            FooterTextBlock.FontFamily = textEditorFontFamily;
            FooterTextBlock.FontSize = textEditorFontSize;
        }
        else
        {
            Footer.Visibility = Visibility.Collapsed;
            FooterTextBlock.FontFamily = textEditorFontFamily;
            FooterTextBlock.FontSize = textEditorFontSize;
        }

        var run = new Run { Text = textEditorText };
        TextEditorContent.Inlines.Add(run);
    }
}
