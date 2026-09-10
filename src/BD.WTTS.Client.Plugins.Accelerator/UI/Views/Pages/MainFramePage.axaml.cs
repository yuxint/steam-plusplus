using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Media.Animation;

namespace BD.WTTS.UI.Views.Pages;

public partial class MainFramePage : UserControl
{
    int lastIndex = -1;

    public MainFramePage()
    {
        InitializeComponent();

        Tabs.SelectionChanged += Tabs_SelectionChanged;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // TabStrip 不会总是自动选中首项，落地时显式选中，否则 InnerNavFrame 为空、页面空白
        if (Tabs.SelectedItem == null && Tabs.ItemCount > 0)
            Tabs.SelectedIndex = 0;
        Tabs_SelectionChanged(null, null);
    }

    private void Tabs_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs? e)
    {
        if (Tabs.SelectedItem is TabStripItem tab && tab.Tag is Type t)
        {
            InnerNavFrame.Navigate(t, null, new SlideNavigationTransitionInfo
            {
                Effect = FrameEffect.GetEffect(lastIndex, Tabs.SelectedIndex),
                FromHorizontalOffset = 70,
            });

            lastIndex = Tabs.SelectedIndex;
        }
    }
}
