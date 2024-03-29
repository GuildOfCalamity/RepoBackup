﻿using System;
using System.ComponentModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIDemo.Controls;

[TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
[TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
public class IsEnabledTextBlock : Control
{
    public IsEnabledTextBlock()
    {
        this.DefaultStyleKey = typeof(IsEnabledTextBlock);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        IsEnabledChanged -= IsEnabledTextBlock_IsEnabledChanged;
        SetEnabledState();
        IsEnabledChanged += IsEnabledTextBlock_IsEnabledChanged;
        base.OnApplyTemplate();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
       "Text",
       typeof(string),
       typeof(IsEnabledTextBlock),
       null);

    [Localizable(true)]
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    void IsEnabledTextBlock_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SetEnabledState();
    }

    void SetEnabledState()
    {
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
    }
}
