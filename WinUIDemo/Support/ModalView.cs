using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIDemo;

/// <summary>
/// Provides elementary Modal View services: display message, request confirmation, request input.
/// All dialogs are based on the <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> control.
/// </summary>
public static class ModalView
{
    public static async Task MessageDialogAsync(this FrameworkElement element, string title, string message)
    {
        await MessageDialogAsync(element, title, message, "OK");
    }

    public static async Task MessageDialogAsync(this FrameworkElement element, string title, string message, string buttonText)
    {
        if (element == null)
            return;

        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = buttonText,
                XamlRoot = element.XamlRoot,
                RequestedTheme = element.ActualTheme
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
    }

    public static async Task<bool?> ConfirmationDialogAsync(this FrameworkElement element, string title)
    {
        return await ConfirmationDialogAsync(element, title, "OK", string.Empty, "Cancel");
    }

    public static async Task<bool> ConfirmationDialogAsync(this FrameworkElement element, string title, string yesButtonText, string noButtonText)
    {
        return (await ConfirmationDialogAsync(element, title, yesButtonText, noButtonText, string.Empty)).Value;
    }

    public static async Task<bool?> ConfirmationDialogAsync(this FrameworkElement element, string title, string yesButtonText, string noButtonText, string cancelButtonText)
    {
        if (element == null)
            return null;

        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = yesButtonText,
            SecondaryButtonText = noButtonText,
            CloseButtonText = cancelButtonText,
            XamlRoot = element.XamlRoot,
            RequestedTheme = element.ActualTheme
        };

        try
        {
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.None)
                return null;

            return (result == ContentDialogResult.Primary);
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }

        return null;
    }

    public static async Task<string> InputStringDialogAsync(this FrameworkElement element, string title)
    {
        return await element.InputStringDialogAsync(title, string.Empty);
    }

    public static async Task<string> InputStringDialogAsync(this FrameworkElement element, string title, string defaultText)
    {
        return await element.InputStringDialogAsync(title, defaultText, "OK", "Cancel");
    }

    public static async Task<string> InputStringDialogAsync(this FrameworkElement element, string title, string defaultText, string okButtonText, string cancelButtonText)
    {
        if (element == null)
            return string.Empty;

        var inputTextBox = new TextBox
        {
            AcceptsReturn = false,
            Height = 32,
            Text = defaultText,
            SelectionStart = defaultText.Length
        };
        var dialog = new ContentDialog
        {
            Content = inputTextBox,
            Title = title,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = okButtonText,
            SecondaryButtonText = cancelButtonText,
            XamlRoot = element.XamlRoot,
            RequestedTheme = element.ActualTheme
        };

        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                return inputTextBox.Text;
            else
                return string.Empty;
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }

        return string.Empty;
    }

    public static async Task<string> InputTextDialogAsync(this FrameworkElement element, string title)
    {
        return await element.InputTextDialogAsync(title, string.Empty);
    }

    public static async Task<string> InputTextDialogAsync(this FrameworkElement element, string title, string defaultText)
    {
        if (element == null)
            return string.Empty;

        var inputTextBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 32 * 6,
            Text = defaultText,
            TextWrapping = TextWrapping.Wrap,
            SelectionStart = defaultText.Length
        };
        var dialog = new ContentDialog
        {
            Content = inputTextBox,
            Title = title,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = "Ok",
            SecondaryButtonText = "Cancel",
            XamlRoot = element.XamlRoot,
            RequestedTheme = element.ActualTheme
        };

        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                return inputTextBox.Text;
            else
                return string.Empty;
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }

        return string.Empty;
    }
}
