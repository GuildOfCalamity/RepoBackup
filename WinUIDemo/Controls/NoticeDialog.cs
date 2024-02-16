using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Popups;

namespace WinUIDemo.Controls;

#region [Base Helper]
public class NoticeDialog : ContentDialog
{
    public bool IsAborted = false;

    readonly SolidColorBrush _darkModeBackgroundBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 10, 10, 10));
    readonly SolidColorBrush _lightModeBackgroundBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 245, 245));

    public NoticeDialog()
    {
        Background = App.Current.RequestedTheme == ApplicationTheme.Dark ? _darkModeBackgroundBrush : _lightModeBackgroundBrush;
        ActualThemeChanged += OnActualThemeChanged;
    }

    void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        Background = ActualTheme == ElementTheme.Dark ? _darkModeBackgroundBrush : _lightModeBackgroundBrush;
    }

    internal static Style GetButtonStyle(Windows.UI.Color backgroundColor)
    {
        var buttonStyle = new Microsoft.UI.Xaml.Style(typeof(Button));
        buttonStyle.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(4d)));
        buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty, backgroundColor));
        buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty, Colors.White));
        return buttonStyle;
    }
}
#endregion

#region [Inherited Samples]
public class GenericDialog : NoticeDialog
{
    public GenericDialog(string yesText, Action yesAction, string noText, Action noAction, string cancelText, Action cancelAction, string title, string content)
    {
        Title = title;
        HorizontalAlignment = HorizontalAlignment.Center;
        Content = content;
        PrimaryButtonText = yesText ?? "Yes";
        SecondaryButtonText = noText ?? "No";
        CloseButtonText = cancelText ?? "Cancel";
        PrimaryButtonStyle = GetButtonStyle(Windows.UI.Color.FromArgb(255, 38, 114, 201)); // light blue (#FF2672C9)

        // Configure event delegates
        PrimaryButtonClick += (dialog, eventArgs) => yesAction();
        SecondaryButtonClick += (dialog, eventArgs) => noAction();
        CloseButtonClick += (dialog, eventArgs) => cancelAction();
    }
}

public class SaveCloseDiscardDialog : NoticeDialog
{
    public SaveCloseDiscardDialog(Action saveAndExitAction, Action discardAndExitAction, Action cancelAction, string content)
    {
        Title = App.AppName;
        HorizontalAlignment = HorizontalAlignment.Center;
        Content = content;
        PrimaryButtonText = "Save";
        SecondaryButtonText = "Discard";
        CloseButtonText = "Close";
        PrimaryButtonStyle = GetButtonStyle(Windows.UI.Color.FromArgb(255, 38, 114, 201)); // light blue (#FF2672C9)

        // Configure event delegates
        PrimaryButtonClick += (dialog, eventArgs) => saveAndExitAction();
        SecondaryButtonClick += (dialog, eventArgs) => discardAndExitAction();
        CloseButtonClick += (dialog, eventArgs) => cancelAction();
    }
}
#endregion

#region [Driver Helper]
public static class DialogManager
{
    public static NoticeDialog ActiveDialog;

    private static TaskCompletionSource<bool> _dialogAwaiter = new TaskCompletionSource<bool>();

    public static async Task<ContentDialogResult?> OpenDialogAsync(NoticeDialog dialog, bool awaitPreviousDialog)
    {
        try
        {
            // NOTE: We must set the XamlRoot in WinUI3 (this was not needed in UWP)
            if (App.MainRoot != null)
                dialog.XamlRoot = App.MainRoot.XamlRoot;

            return await OpenDialog(dialog, awaitPreviousDialog);
        }
        catch (Exception ex)
        {
            var activeDialogTitle = string.Empty;
            var pendingDialogTitle = string.Empty;

            if (ActiveDialog?.Title is string activeTitle)
                activeDialogTitle = activeTitle;

            if (dialog?.Title is string pendingTitle)
                pendingDialogTitle = pendingTitle;

            Debug.WriteLine($"FailedToOpenDialog: {ex.Message}");
        }

        return null;
    }

    static async Task<ContentDialogResult> OpenDialog(NoticeDialog dialog, bool awaitPreviousDialog)
    {
        TaskCompletionSource<bool> currentAwaiter = _dialogAwaiter;
        TaskCompletionSource<bool> nextAwaiter = new TaskCompletionSource<bool>();
        _dialogAwaiter = nextAwaiter;

        // Check for previous dialogs.
        if (ActiveDialog != null)
        {
            if (awaitPreviousDialog)
            {
                await currentAwaiter.Task;
            }
            else
            {
                ActiveDialog.IsAborted = true;
                ActiveDialog.Hide();
            }
        }

        ActiveDialog = dialog;

        // Show the dialog.
        try 
        { 
            return await ActiveDialog.ShowAsync(ContentDialogPlacement.Popup); 
        }
        finally 
        { 
            nextAwaiter.SetResult(true); 
        }
    }
}
#endregion
