using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Windows.System;

namespace WinUIDemo.Controls;

/// <summary>
/// A simple input <see cref="TextBox"/> control that provides custom functionality.
/// </summary>
public sealed partial class TextInputControl : UserControl
{
    #region [Properties]
    private string _currentLine = "";
    private int _maxLine = 50;
    private bool _onlyNumbers = false;

    // We expose these public events so they can be seen by the XAML page that employs our control.
    public event EventHandler<RoutedEventArgs> OnDismissKeyDown;
    public event EventHandler<TextInputEventArgs> OnTextInputButtonClicked;
    public event EventHandler<KeyRoutedEventArgs> OnTextInputKeyDown;

    /// <summary>
    /// Prompt text shown on the left of input box.
    /// </summary>
    public string Title
    {
        get { return GetValue(TitleProperty) as string; }
        set { SetValue(TitleProperty, value); }
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(TabHeader),
        null);
    #endregion

    #region [Public Methods]
    public TextInputControl()
    {
        InitializeComponent();
        SetSelectionHighlightColor(MainWindow.BackdropColor);
        Loaded += TextInputControlOnLoaded;
        //this.ActualThemeChanged += TextInputControlOnActualThemeChanged;
    }

    public void Dispose()
    {
        Loaded -= TextInputControlOnLoaded;
        //this.ActualThemeChanged -= TextInputControlOnActualThemeChanged;
    }

    public void SetInputData(string currentLine, int maxLine, bool onlyNumbers)
    {
        _currentLine = currentLine;
        _maxLine = maxLine;
        _onlyNumbers = onlyNumbers;
    }

    public void ClearInputData()
    {
        TextInputBar.Text = _currentLine = "";
    }

    public double GetHeight()
    {
        return TextInputRootGrid.Height;
    }

    public void Focus(bool selectAll = false)
    {
        TextInputBar.Text = _currentLine;
        TextInputBar.Focus(FocusState.Programmatic);
        if (selectAll && TextInputBar.Text.Length > 0) 
        {
            TextInputBar.SelectAll();
        }
    }
    #endregion

    #region [Private Methods]
    void TextInputControlOnLoaded(object sender, RoutedEventArgs e)
    {
        tbTitle.Text = Title;
        Focus();
    }

    async void TextInputControlOnActualThemeChanged(FrameworkElement sender, object args)
    {
        await DispatcherQueue.EnqueueAsync(() =>
        {
            SetSelectionHighlightColor(MainWindow.BackdropColor);
        });
    }

    void SetSelectionHighlightColor(Windows.UI.Color color)
    {
        TextInputBar.SelectionHighlightColor = new SolidColorBrush(color);
        TextInputBar.SelectionHighlightColorWhenNotFocused = new SolidColorBrush(color);
    }

    void TextInputBarOnTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchButton.Visibility = !string.IsNullOrEmpty(TextInputBar.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    void SearchButtonOnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyout) return;

        OnTextInputButtonClicked?.Invoke(sender, new TextInputEventArgs(TextInputBar.Text));
    }

    void TextInputBarOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !string.IsNullOrEmpty(TextInputBar.Text))
        {
            SearchButtonOnClick(sender, e);
        }

        if (e.Key == VirtualKey.Tab)
        {
            e.Handled = true;
        }
    }

    void TextInputBarOnGotFocus(object sender, RoutedEventArgs e)
    {
        TextInputBar.SelectionStart = 0;
        TextInputBar.SelectionLength = TextInputBar.Text.Length;
    }

    void TextInputBarOnLostFocus(object sender, RoutedEventArgs e)
    {
        TextInputBar.SelectionStart = TextInputBar.Text.Length;
    }

    void DismissButtonOnClick(object sender, RoutedEventArgs e)
    {
        OnDismissKeyDown?.Invoke(sender, e);
    }

    /// <summary>
    /// We can filter undesirable chars here.
    /// </summary>
    void TextInputBarOnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (string.IsNullOrEmpty(args.NewText)) return;

        int line = 0;

        if (_onlyNumbers && !int.TryParse(args.NewText, out line) || args.NewText.Contains(" "))
        {
            Debug.WriteLine("TextInputControlError: InvalidInput");
            args.Cancel = true;
        }
        else if (_onlyNumbers && (line > _maxLine || line <= 0))
        {
            Debug.WriteLine("TextInputControlError: ExceedInputLimit");
            args.Cancel = true;
        }
        else if (!_onlyNumbers && (args.NewText.Length > _maxLine))
        {
            Debug.WriteLine("TextInputControlError: ExceedInputLimit");
            args.Cancel = true;
        }
    }

    void TextInputRootGridOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!e.Handled)
        {
            OnTextInputKeyDown?.Invoke(sender, e);
        }
    }
    #endregion
}
