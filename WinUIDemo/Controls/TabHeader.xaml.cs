using System;
using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace WinUIDemo.Controls;

public sealed partial class TabHeader : UserControl
{
    bool _showOutline = false;
    SolidColorBrush _hoverBrush = new SolidColorBrush();

    #region [Properties]
    public static readonly DependencyProperty ShowOutlineProperty = DependencyProperty.Register(
        nameof(ShowOutline),
        typeof(string),
        typeof(TabHeader),
        new PropertyMetadata("False"));
    public string ShowOutline
    {
        get { return GetValue(ShowOutlineProperty) as string; }
        set { SetValue(ShowOutlineProperty, value); }
    }

    public static readonly DependencyProperty SelectedImageProperty = DependencyProperty.Register(
        nameof(SelectedImage), 
        typeof(string), 
        typeof(TabHeader), 
        null);
    public string SelectedImage
    {
        get { return GetValue(SelectedImageProperty) as string; }
        set { SetValue(SelectedImageProperty, value); }
    }

    public static readonly DependencyProperty UnselectedImageProperty = DependencyProperty.Register(
        nameof(UnselectedImage), 
        typeof(string), 
        typeof(TabHeader),
        null);

    public string UnselectedImage
    {
        get { return GetValue(UnselectedImageProperty) as string; }
        set { SetValue(UnselectedImageProperty, value); }
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), 
        typeof(string), 
        typeof(TabHeader), 
        null);

    public string Label
    {
        get { return GetValue(LabelProperty) as string; }
        set { SetValue(LabelProperty, value); }
    }
    #endregion

    public TabHeader()
    {
        this.InitializeComponent();
        this.Loaded += TabHeaderOnLoaded;
        this.PointerEntered += TabHeaderOnPointerEntered;
        this.PointerExited += TabHeaderOnPointerExited;
        DataContext = this;
    }

    #region [Events]
    /// <summary>
    /// Demonstrate fetching brush resource using our <see cref="Extensions.GetResource{T}"/> method.
    /// </summary>
    void TabHeaderOnLoaded(object sender, RoutedEventArgs e)
    {
        _hoverBrush = Extensions.GetResource<SolidColorBrush>("QuaternaryBrush");
    }

    /// <summary>
    /// Change the style if mouse enters the control bounds.
    /// There are many visual options here to experiment with.
    /// </summary>
    void TabHeaderOnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ShowOutline) && ShowOutline.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            stackPanel.BorderBrush = _hoverBrush;
            stackPanel.BorderThickness = new Thickness(2);
        }
    }

    /// <summary>
    /// Change the style if mouse leaves the control bounds.
    /// There are many visual options here to experiment with.
    /// </summary>
    void TabHeaderOnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ShowOutline) && ShowOutline.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            stackPanel.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            stackPanel.BorderThickness = new Thickness(0);
        }
    }
    #endregion

    /// <summary>
    /// Changes the image visible state during swap.
    /// </summary>
    /// <param name="isSelected"></param>
    public void SetSelectedItem(bool isSelected)
    {
        if (isSelected)
        {
            selectedImage.Visibility = Visibility.Visible;
            unselectedImage.Visibility = Visibility.Collapsed;
        }
        else
        {
            selectedImage.Visibility = Visibility.Collapsed;
            unselectedImage.Visibility = Visibility.Visible;
        }
    }
}
