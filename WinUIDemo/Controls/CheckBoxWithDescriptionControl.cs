using System;
using System.ComponentModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace WinUIDemo.Controls;

public class CheckBoxWithDescriptionControl : CheckBox
{
    CheckBoxWithDescriptionControl _checkBoxSubTextControl;

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(string),
        typeof(CheckBoxWithDescriptionControl),
        new PropertyMetadata(default(string)));

    [Localizable(true)]
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(CheckBoxWithDescriptionControl),
        new PropertyMetadata(default(string)));

    [Localizable(true)]
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public CheckBoxWithDescriptionControl()
    {
        _checkBoxSubTextControl = (CheckBoxWithDescriptionControl)this;
        this.Loaded += CheckBoxSubTextControl_Loaded;
    }

    protected override void OnApplyTemplate()
    {
        Update();
        base.OnApplyTemplate();
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(Header))
        {
            AutomationProperties.SetName(this, Header);
        }
    }

    void CheckBoxSubTextControl_Loaded(object sender, RoutedEventArgs e)
    {
        StackPanel panel = new StackPanel() { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical };
        panel.Children.Add(new TextBlock() { Text = Header, TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords });

        // Add text box only if the description is not empty.
        if (!string.IsNullOrWhiteSpace(Description))
        {
            panel.Children.Add(new IsEnabledTextBlock()
            {
                Text = Description,
                Style = (Style)App.Current.Resources["SecondaryIsEnabledTextBlockStyle"],
            });
        }

        _checkBoxSubTextControl.Content = panel;
    }
}
