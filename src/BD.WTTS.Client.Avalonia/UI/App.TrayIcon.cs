using Avalonia.Controls;
using Avalonia.Threading;
using BD.WTTS.Client.Resources;
using System.Collections.Specialized;
using static BD.WTTS.Services.INotificationService;

namespace BD.WTTS.UI;

partial class App
{
    public Dictionary<string, TrayMenuItem> TrayMenus { get; } = new Dictionary<string, TrayMenuItem>();

    public TrayIcons TrayIcons { get; init; } = new TrayIcons();

    readonly Dictionary<string, ICommand> trayIconMenus = new();

    public IReadOnlyDictionary<string, ICommand> TrayIconMenus => trayIconMenus;

    void InitTrayIcon()
    {
        if (ApplicationLifetime is not
            IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime)
            return;

        var s = Startup.Instance;
        if (s.IsMainProcess)
        {
            s.HasTrayIcon =
#if MACOS
                true; // macOS 常驻菜单栏图标：窗口关闭后仅能从菜单栏退出
#else
                GeneralSettings.TrayIcon.Value;
#endif
            if (s.HasTrayIcon)
            {
                TrayIcon.SetIcons(this, TrayIcons);
                TrayIcons.Add(new TrayIcon
                {
                    Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://BD.WTTS.Client.Avalonia/UI/Assets/ApplicationIcon.ico"))),
                    ToolTipText = AssemblyInfo.Trademark,
                    Command = ReactiveCommand.Create(RestoreMainWindow),
                    //[!TrayIcon.IsVisibleProperty] = new Binding { Path = "Value", Source = GeneralSettings.IsEnableTrayIcon, Mode = BindingMode.OneWay },
                    Menu = new NativeMenu(),
                });

                UpdateMenuItems();
#if MACOS
                // 菜单栏图标改为 template 单色图标：启动时 NSStatusItem 可能尚未创建，重试应用
                StartTrayAccelerateTemplateImageRetry();
#endif

                //IViewModelManager.Instance.InitTaskBarWindowViewModel();
                //NotifyIconHelper.Init(this,
                //    notifyIconClick: (_, _) => RestoreMainWindow());
            }
            else
            {
                foreach (var trayIcon in TrayIcons)
                {
                    trayIcon.Dispose();
                }
                TrayIcons.Clear();
                TrayIcon.SetIcons(this, null);
                //NotifyIconHelper.Dispoe();
                //IViewModelManager.Instance.DispoeTaskBarWindowViewModel();
            }

            classicDesktopStyleApplicationLifetime.ShutdownMode =
#if UI_DEMO
                        ShutdownMode.OnMainWindowClose;
#else
                s.HasTrayIcon ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnMainWindowClose;
#endif
#if MACOS
            if (Current!.TryGetFeature<IActivatableLifetime>() is { } activatableLifetime)
            {
                activatableLifetime.Activated += ActivatableLifetimeOnActivated;
                // macOS 中 右键隐藏 会触发目前不需要使用
                //activatableLifetime.Deactivated += ActivatableLifetimeOnDeactivated;
            }
#endif
        }
    }

#if MACOS
    /// <summary>
    /// 从 macOS Dock 栏恢复窗口显示
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ActivatableLifetimeOnActivated(object? sender, ActivatedEventArgs e)
    {
        switch (e.Kind)
        {
            case ActivationKind.Background:
            case ActivationKind.Reopen:
                RestoreMainWindow();
                break;
        }
    }

    ///// <summary>
    ///// macOS 中 右键隐藏 会触发目前不需要使用
    ///// </summary>
    ///// <param name="sender"></param>
    ///// <param name="e"></param>
    //private void ActivatableLifetimeOnDeactivated(object? sender, ActivatedEventArgs e)
    //{
    //    switch (e.Kind)
    //    {
    //        case ActivationKind.Background:
    //            Console.WriteLine($"ActivatableLifetimeOnDeactivated {e.Kind}");
    //            break;
    //    }
    //}
#endif

#if MACOS
    /// <summary>
    /// 当前待应用的菜单栏图标加速状态；NSStatusItem 按钮可能尚未创建，先记录状态待按钮就绪后应用
    /// </summary>
    static bool trayIconAccelerating;

    /// <summary>
    /// 应用菜单栏图标加速状态视觉：加速中正常显色、未加速灰显（template 图标以 alpha 表达）
    /// </summary>
    /// <returns>图标是否已应用（按钮尚未创建时返回 false）</returns>
    static bool ApplyTrayAccelerateTemplateImage()
    {
        var uri = new Uri(trayIconAccelerating
            ? "avares://BD.WTTS.Client.Avalonia/UI/Assets/TrayAccelerateOn.png"
            : "avares://BD.WTTS.Client.Avalonia/UI/Assets/TrayAccelerateOff.png");
        using var stream = AssetLoader.Open(uri);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return ApplyTrayTemplateImage(memory.ToArray());
    }

    /// <summary>
    /// 启动时 NSStatusItem 可能尚未由 Avalonia 创建，定时重试应用 template 图标直到成功
    /// </summary>
    static void StartTrayAccelerateTemplateImageRetry()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var attempts = 0;
        timer.Tick += (_, _) =>
        {
            attempts++;
            if (ApplyTrayAccelerateTemplateImage() || attempts >= 20)
                timer.Stop();
        };
        timer.Start();
    }
#endif

    /// <summary>
    /// 更新菜单栏图标加速状态：加速中正常显色，未加速灰显
    /// </summary>
    public void UpdateTrayIconStatus(bool isAccelerating)
    {
#if MACOS
        trayIconAccelerating = isAccelerating;
        MainThread2.BeginInvokeOnMainThread(() => ApplyTrayAccelerateTemplateImage());
#endif
    }

    public void UpdateMenuItems()
    {
        MainThread2.BeginInvokeOnMainThread(() =>
        {
            var menus = TrayIcons.FirstOrDefault()?.Menu;

            if (menus != null)
            {
                menus.Items.Clear();

                var defaultTrayMenus = new List<NativeMenuItemBase>()
                    {
                        new NativeMenuItem
                        {
                            [!NativeMenuItem.HeaderProperty] = new Binding { Path = "Res.OpenMainWindow", Source = ResourceService.Current, Mode = BindingMode.OneWay },
                            Command = ReactiveCommand.Create(RestoreMainWindow),
                        },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem
                        {
                            [!NativeMenuItem.HeaderProperty] = new Binding { Path = "Res.Exit", Source = ResourceService.Current, Mode = BindingMode.OneWay },
                            Command = ReactiveCommand.Create(() => { Shutdown(); })
                        },
                    };

                if (TrayMenus.Any_Nullable())
                {
                    foreach (var item in TrayMenus)
                    {
                        NativeMenu? subMenu = null;
                        if (item.Value.Items.Any_Nullable())
                        {
                            subMenu = new NativeMenu();
                            foreach (var sub in item.Value.Items)
                            {
                                var menu = new NativeMenuItem
                                {
                                    Header = sub.Name,
                                    Command = sub.Command,
                                    CommandParameter = sub.CommandParameter,
                                };

                                if (sub.IsVisible is Avalonia.Data.IBinding v)
                                    menu[!NativeMenuItem.IsVisibleProperty] = v;
                                if (sub.IsEnabled is Avalonia.Data.IBinding e)
                                    menu[!NativeMenuItem.IsEnabledProperty] = e;
                                subMenu.Add(menu);
                            }
                            menus.Add(new NativeMenuItem
                            {
                                Header = item.Value.Name,
                                Menu = subMenu,
                            });
                        }
                        else
                        {
                            var menu = new NativeMenuItem
                            {
                                Header = item.Value.Name,
                                Command = item.Value.Command,
                                CommandParameter = item.Value.CommandParameter,
                            };
                            if (item.Value.IsVisible is Avalonia.Data.IBinding visibleBinding)
                                menu[!NativeMenuItem.IsVisibleProperty] = visibleBinding;
                            else if (item.Value.IsVisible is bool isVisible)
                                menu.IsVisible = isVisible;
                            if (item.Value.IsEnabled is Avalonia.Data.IBinding enabledBinding)
                                menu[!NativeMenuItem.IsEnabledProperty] = enabledBinding;
                            else if (item.Value.IsEnabled is bool isEnabled)
                                menu.IsEnabled = isEnabled;
                            menus.Add(menu);
                        }
                    }
                    menus.Add(new NativeMenuItemSeparator());
                }

                foreach (var item in defaultTrayMenus)
                {
                    menus.Add(item);
                }
            }
        });
    }

    public void UpdateMenuItems(string menuKey, TrayMenuItem trayMenuItem)
    {
        if (TrayMenus != null)
        {
            if (TrayMenus.ContainsKey(menuKey))
                TrayMenus[menuKey] = trayMenuItem;
            else
                TrayMenus.Add(menuKey, trayMenuItem);

            UpdateMenuItems();
        }
    }
}