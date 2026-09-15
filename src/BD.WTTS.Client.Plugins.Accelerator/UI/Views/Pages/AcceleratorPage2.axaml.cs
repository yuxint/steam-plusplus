using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;

namespace BD.WTTS.UI.Views.Pages;

/// <summary>
/// 网络加速页面
/// </summary>
public partial class AcceleratorPage2 : PageBase<AcceleratorPageViewModel>
{
    readonly List<int> acceleratorTabsSelectedIndexs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AcceleratorPage2"/> class.
    /// </summary>
    public AcceleratorPage2()
    {
        InitializeComponent();
        this.SetViewModel<AcceleratorPageViewModel>(true);

        for (int i = 0; i < AcceleratorTabs.Items.Count; i++)
        {
            var item = AcceleratorTabs.Items[i];
            if (item is Visual visual && visual.IsVisible)
            {
                acceleratorTabsSelectedIndexs.Add(i);
            }
        }
        var itemCount = AcceleratorTabs.ItemCount;
        var acceleratorTabsSelectedIndex = ProxySettings.AcceleratorTabsSelectedIndex.Value;
        if (acceleratorTabsSelectedIndex >= 0 && acceleratorTabsSelectedIndex < itemCount)
        {
            if (acceleratorTabsSelectedIndexs.Contains(acceleratorTabsSelectedIndex))
            {
                AcceleratorTabs.SelectedIndex = acceleratorTabsSelectedIndex;
            }
        }

        AcceleratorTabs.SelectionChanged += AcceleratorTabs_SelectionChanged;

        this.WhenActivated(disposables =>
        {
            disposables.Add(
                ProxyService.Current.WhenValueChanged(x => x.ProxyStatus, false)
                    .Subscribe(x =>
                    {
                        AcceleratorTabs.SelectedIndex = x ? 1 : 0;
                    }));
        });
    }

    void AcceleratorTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var value = AcceleratorTabs.SelectedIndex;
        if (acceleratorTabsSelectedIndexs.Contains(value))
        {
            ProxySettings.AcceleratorTabsSelectedIndex.Value = value;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (AcceleratorTabs.SelectedIndex == default && ProxyService.Current.ProxyStatus)
            AcceleratorTabs.SelectedIndex = 1;
    }
}