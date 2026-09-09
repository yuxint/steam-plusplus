// ReSharper disable once CheckNamespace
using Avalonia.Data;
using BD.WTTS.Helpers;
using Google.Protobuf.WellKnownTypes;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Linq;

namespace BD.WTTS.Services;

public sealed partial class ProxyService
#if (WINDOWS || MACCATALYST || MACOS || LINUX) && !(IOS || ANDROID)
    : ReactiveObject, IProxyService
#endif
{
    static readonly Lazy<ProxyService> mCurrent = new(() => new(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static ProxyService Current => mCurrent.Value;

    readonly IReverseProxyService reverseProxyService = IReverseProxyService.Constants.Instance;
    readonly IHostsFileService hostsFileService = IHostsFileService.Constants.Instance;
    readonly IPlatformService platformService = IPlatformService.Instance;

    ProxyService()
    {
        ProxyDomains = new SourceCache<AccelerateProjectGroupDTO, string>(s => s.Name);

        ProxyDomains
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Sort(SortExpressionComparer<AccelerateProjectGroupDTO>.Ascending(x => x.Order).ThenBy(x => x.Name))
            .Bind(out _ProxyDomainsList)
            .Subscribe(_ => SelectGroup = ProxyDomains.Items.FirstOrDefault());

        //this.WhenValueChanged(x => x.ProxyStatus, false)
        //    .ObserveOn(RxApp.MainThreadScheduler)
        //    .Subscribe(async proxyStatusLeft =>
        //    {
        //        bool proxyStatusRight;
        //        if (proxyStatusLeft)
        //        {
        //            var reuslt = await StartProxyServiceAsync();
        //            proxyStatusRight = reuslt.OnStartedShowToastReturnProxyStatus();
        //        }
        //        else
        //        {
        //            var reuslt = await StopProxyServiceAsync();
        //            proxyStatusRight = reuslt.OnStopedShowToastReturnProxyStatus();
        //        }
        //        if (proxyStatusLeft != proxyStatusRight)
        //        {
        //            ProxyStatus = proxyStatusRight;

        //            //UpdateProxyTrayMenuItems();
        //            //if (Steamworks.SteamClient.IsValid)
        //            //{
        //            //    if (ProxyStatus)
        //            //        Steamworks.SteamFriends.SetRichPresence("steam_display", "#Status_Accelerator");
        //            //    else
        //            //        Steamworks.SteamFriends.ClearRichPresence();
        //            //}
        //        }
        //    });

        this.WhenAnyValue(v => v.ProxyDomainsList)
              .ObserveOn(RxApp.MainThreadScheduler)
              .Subscribe(domain => domain?
              .ToObservableChangeSet()
              .AutoRefresh(x => x.ObservableItems)
              .TransformMany(t => t.ObservableItems ?? new ObservableCollection<AccelerateProjectDTO>())
              .AutoRefresh(x => x.ThreeStateEnable)
              .WhenPropertyChanged(x => x.ThreeStateEnable, false)
              .Subscribe(_ =>
              {
                  IsChangeSupportProxyServicesStatus = true;
                  ProxySettings.SupportProxyServicesStatus.Value = GetAccelerateEnableAllIds(EnableProxyDomains).ToImmutableHashSet();
              }));
    }

    public SourceCache<AccelerateProjectGroupDTO, string> ProxyDomains { get; }

    private readonly ReadOnlyObservableCollection<AccelerateProjectGroupDTO> _ProxyDomainsList;

    public ReadOnlyObservableCollection<AccelerateProjectGroupDTO> ProxyDomainsList => _ProxyDomainsList;

    private AccelerateProjectGroupDTO? _SelectGroup;

    public AccelerateProjectGroupDTO? SelectGroup
    {
        get => _SelectGroup;
        set => this.RaiseAndSetIfChanged(ref _SelectGroup, value);
    }

    public async Task StartOrStopProxyService(bool startOrStop)
    {
        if (startOrStop == ProxyStatus)
        {
            return;
        }
        if (!ProxyStarting)
        {
            ProxyStarting = true;

            bool proxyStatusRight;
            if (startOrStop)
            {
                var reuslt = await StartProxyServiceAsync();
                proxyStatusRight = reuslt.OnStartedShowToastReturnProxyStatus();
            }
            else
            {
                var reuslt = await StopProxyServiceAsync();
                proxyStatusRight = reuslt.OnStopedShowToastReturnProxyStatus();
            }

            if (startOrStop != proxyStatusRight)
            {
                startOrStop = proxyStatusRight;
            }

            //UpdateProxyTrayMenuItems();
            //if (Steamworks.SteamClient.IsValid)
            //{
            //    if (startOrStop)
            //        Steamworks.SteamFriends.SetRichPresence("steam_display", "#Status_Accelerator");
            //    else
            //        Steamworks.SteamFriends.ClearRichPresence();
            //}

            ProxyStarting = false;
            ProxyStatus = startOrStop;
        }
    }

    public IEnumerable<AccelerateProjectDTO> GetEnableProxyDomains()
    {
        if (!ProxyDomains.Items.Any_Nullable())
            return [];
        var data = ProxyDomains.Items
            .Where(x => x.Items != null)
            .SelectMany(s => s.Items!.Where(w => w.ThreeStateEnable != false))
            .Select(item =>
            {
                //过滤部分选中的子项
                if (item.Items.Any_Nullable())
                {
                    return new AccelerateProjectDTO
                    {
                        Name = item.Name,
                        Port = item.Port,
                        MatchDomainNames = item.MatchDomainNames,
                        ForwardDomainNames = item.ForwardDomainNames,
                        IgnoreSSLCertVerification = item.IgnoreSSLCertVerification,
                        FakeServerName = item.FakeServerName,
                        ProxyType = item.ProxyType,
                        ListenDomainNames = item.ListenDomainNames,
                        Checked = item.Checked,
                        Id = item.Id,
                        Order = item.Order,
                        FakeUserAgent = item.FakeUserAgent,
                        Version = item.Version,
                        ThreeStateEnable = item.ThreeStateEnable,
                        // 递归过滤子项
                        Items = item.Items.Where(x => x.ThreeStateEnable != false).ToList()
                    };
                }
                return item;
            });

        return data;
    }

    public IReadOnlyCollection<AccelerateProjectDTO>? EnableProxyDomains => GetEnableProxyDomains().ToImmutableArray();

    //static IEnumerable<AccelerateProjectDTO>? GetProxyDomainsItems(AccelerateProjectDTO accelerates)
    //{
    //    return accelerates.Items.Where(w => w.Enable).SelectMany(GetProxyDomainsItems);
    //}

    static void EnableProxyDomainsItems(AccelerateProjectDTO accelerates)
    {
        if (accelerates.Items != null)
        {
            foreach (var item in accelerates.Items)
            {
                item.Checked = accelerates.Checked;
                EnableProxyDomainsItems(item);
            }
        }
    }

    private DateTimeOffset _StartAccelerateTime;

    [Reactive]
    public string? IPv6AddresString { get; set; }

    [Reactive]
    public TimeSpan AccelerateTime { get; set; }

    #region HOSTS_PROXY_RUNNING_STATUS

    //const string KEY_HOSTS_PROXY_RUNNING_STATUS = "KEY_HOSTS_PROXY_RUNNING_STATUS";
    //static async void SaveHostsProxyStatus(bool value)
    //{
    //    await ISecureStorage.Instance.SetAsync<bool>(KEY_HOSTS_PROXY_RUNNING_STATUS, value);
    //}

    //public static async Task<bool> GetHostsProxyStatusAsync()
    //{
    //    var r = await ISecureStorage.Instance.GetAsync<bool>(KEY_HOSTS_PROXY_RUNNING_STATUS);
    //    return r;
    //}

    #endregion HOSTS_PROXY_RUNNING_STATUS

    #region 代理状态启动退出

    /// <summary>
    /// 代理启动中
    /// </summary>
    [Reactive]
    public bool ProxyStarting { get; set; }

    [Reactive]
    public bool ProxyStatus { get; set; }

    #endregion 代理状态启动退出

    static bool IsProgramStartupRunProxy()
    {
        var s = Startup.Instance;

        if (s.IsProxyService &&
            (s.ProxyServiceStatus == OnOffToggle.On ||
                s.ProxyServiceStatus == OnOffToggle.Toggle))
            return true;

        return ProxySettings.ProgramStartupRunProxy.Value;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await InitializeAccelerateAsync();
        }
        catch (Exception ex)
        {
            Toast.LogAndShowT(ex, nameof(ProxyService),
                msg: "Accelerate init fail.");
            return; // 加速项目初始化失败时，中止初始化
        }

        try
        {
            await RefreshIpv6Support();
        }
        catch (Exception ex)
        {
            Toast.LogAndShowT(ex, nameof(ProxyService),
                msg: "IPv6 refresh fail.");
            // Ipv6 支持刷新失败时，可忽略
        }

        try
        {
            if (IsProgramStartupRunProxy())
            {
                if (platformService.UsePlatformForegroundService)
                {
                    await platformService.StartOrStopForegroundServiceAsync(nameof(ProxyService), true);
                }
                else
                {
                    //ProxyStatus = true;
                    await StartOrStopProxyService(true);
                }
            }
        }
        catch (Exception ex)
        {
            Toast.LogAndShowT(ex, nameof(ProxyService),
                msg: "Program startup run proxy fail.");
            // 程序启动时启动加速服务失败，可忽略
        }

        UpdateProxyTrayMenuItems();
    }

    private void UpdateProxyTrayMenuItems()
    {
        try
        {
            IApplication.Instance.UpdateMenuItems(Plugin.Instance.UniqueEnglishName, new TrayMenuItem
            {
                Name = Plugin.Instance.Name,
                Items = new List<TrayMenuItem>
                {
                    new TrayMenuItem
                    {
                        Name = "启动",
                        //IsEnabled = new Binding()
                        //{
                        //    Source = ProxyService.Current,
                        //    Mode = BindingMode.OneWay,
                        //    Path = "!" + nameof(ProxyStatus),
                        //},
                        Command = ReactiveCommand.CreateFromTask(async () =>
                        {
                            await StartOrStopProxyService(true);
                        }),
                    },
                    new TrayMenuItem
                    {
                        Name = "停止",
                        //IsEnabled = new Binding()
                        //{
                        //    Source = ProxyService.Current,
                        //    Mode = BindingMode.OneWay,
                        //    Path = nameof(ProxyStatus),
                        //},
                        Command = ReactiveCommand.CreateFromTask(async () =>
                        {
                            await StartOrStopProxyService(false);
                        }),
                    },
                },
            });
        }
        catch (Exception ex)
        {
            ex.LogAndShowT();
            //托盘菜单添加异常
        }
    }

    /// <summary>
    /// 仅保留的加速分组名称白名单：公共 CDN、Google 翻译、Github
    /// </summary>
    static readonly string[] WhitelistGroupNames =
    [
        "公共CDN",
        "Google翻译",
        "Github",
    ];

    public async Task InitializeAccelerateAsync()
    {
        ProxyDomains.Clear();
        // 加载代理服务数据
        var client = IMicroServiceClient.Instance.Accelerate;
#if DEBUG
        var stopwatch = Stopwatch.StartNew();
#endif
        var result = await client.All();
#if DEBUG
        stopwatch.Stop();
        Toast.Show(ToastIcon.Info, Strings.Info_LoadingAgentTakesTime____.Format(stopwatch.ElapsedMilliseconds, result.IsSuccess, result.Code, result.Content?.Count));
#endif
        if (result.IsSuccess && result.Content.Any_Nullable())
        {
            ProxyDomains.AddOrUpdate(FilterWhitelistGroups(result.Content!));
            // 远端可能已删除白名单内的分组（如 Google 翻译），从本地缓存补回
            MergeLocalWhitelistGroups();
        }
        else
        {
            var localAccelerates = LoadLocalAccelerate();
            if (localAccelerates.Any_Nullable())
            {
                ProxyDomains.AddOrUpdate(FilterWhitelistGroups(localAccelerates!));
            }
        }

        SaveLocalAccelerate();

        if (ProxyDomains.Items.Any_Nullable())
        {
            var items = ProxyDomains.Items!.SelectMany(s => s.Items!).ToList();
            var enableItems = ProxySettings.SupportProxyServicesStatus.Value;
            if (enableItems.Any_Nullable())
            {
                RestoreAccelerateEnableAllIds(items, enableItems);
            }
            else
            {
                // 没有已保存的勾选记录（首次使用）时，默认全选保留的分组
                RestoreAccelerateEnableAllIds(items, GetLeafIds(items).ToArray());
            }
        }
    }

    public static bool IsChangeSupportProxyServicesStatus { get; set; }

    static string NormalizeGroupName(string name) =>
        string.Concat(name.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();

    // 注意：此处不能用 LINQ 的 Contains/Any 做字符串比较。
    // publish 裁剪（assembly trimming）后的运行环境中，LINQ 对 string 序列的相等性判断会异常
    // （码点完全相同的字符串被判定不等，导致加速分组白名单恒为空），须手写逐字符比较。
    static bool IsWhitelistedGroupName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var n = NormalizeGroupName(name);
        foreach (var w in WhitelistGroupNames)
        {
            var nw = NormalizeGroupName(w);
            if (nw.Length != n.Length) continue;
            var all = true;
            for (var i = 0; i < nw.Length; i++)
            {
                if (nw[i] != n[i]) { all = false; break; }
            }
            if (all) return true;
        }
        return false;
    }

    static IEnumerable<AccelerateProjectGroupDTO> FilterWhitelistGroups(IEnumerable<AccelerateProjectGroupDTO> groups) =>
        groups.Where(s => IsWhitelistedGroupName(s.Name));

    static IEnumerable<string> GetLeafIds(IEnumerable<AccelerateProjectDTO> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Items.Any_Nullable())
            {
                foreach (var id in GetLeafIds(node.Items))
                    yield return id;
            }
            else
            {
                yield return node.Id.ToString();
            }
        }
    }

    /// <summary>
    /// 从本地缓存补回远端缺失的白名单分组
    /// </summary>
    void MergeLocalWhitelistGroups()
    {
        var localAccelerates = LoadLocalAccelerate();
        if (!localAccelerates.Any_Nullable())
            return;
        var exists = ProxyDomains.Items.Select(s => NormalizeGroupName(s.Name ?? string.Empty)).ToHashSet();
        foreach (var group in FilterWhitelistGroups(localAccelerates!))
        {
            if (!exists.Contains(NormalizeGroupName(group.Name ?? string.Empty)))
            {
                ProxyDomains.AddOrUpdate(group);
            }
        }
    }

    List<AccelerateProjectGroupDTO>? LoadLocalAccelerate()
    {
        var localAccelerateFilePath = Path.Combine(Plugin.Instance.AppDataDirectory,
            "LOCAL_ACCELERATE.json");
        if (File.Exists(localAccelerateFilePath) &&
            IOPath.TryOpenRead(localAccelerateFilePath,
            out var fileStream, out var _))
        {
            using var stream = fileStream;
            try
            {
                return MessagePackSerializer.Deserialize<List<AccelerateProjectGroupDTO>>(stream, options: Serializable.lz4Options);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(ProxyService), ex, nameof(LoadLocalAccelerate));
            }
        }
        return null;
    }

    void SaveLocalAccelerate()
    {
        if (!ProxyDomains.Items.Any_Nullable())
            return;
        var localAccelerateFilePath = Path.Combine(Plugin.Instance.AppDataDirectory,
            "LOCAL_ACCELERATE.json");
        if (IOPath.TryOpen(localAccelerateFilePath,
            FileMode.Create, FileAccess.Write, FileShare.Read,
            out var fileStream, out var _))
        {
            using var stream = fileStream;
            MessagePackSerializer.Serialize(stream, ProxyDomains.Items, options: Serializable.lz4Options);
        }
    }

    private IEnumerable<string> GetAccelerateEnableAllIds(IEnumerable<AccelerateProjectDTO>? nodes)
    {
        if (nodes == null)
            return Enumerable.Empty<string>();
        return nodes.Where(s => s.ThreeStateEnable == true).SelectMany(node => new[] { node.Id.ToString() }.Concat(GetAccelerateEnableAllIds(node.Items)));
    }

    private void RestoreAccelerateEnableAllIds(IEnumerable<AccelerateProjectDTO> nodes, IReadOnlyCollection<string> enableItems)
    {
        foreach (var node in nodes)
        {
            if (node.Items.Any_Nullable())
            {
                RestoreAccelerateEnableAllIds(node.Items, enableItems);
                continue;
            }
            if (enableItems.Contains(node.Id.ToString()))
            {
                node.ThreeStateEnable = true;
            }
        }
    }

    Timer? timer;

    public void StartTimer()
    {
        timer ??= new Timer(_ => AccelerateTime = DateTimeOffset.Now - _StartAccelerateTime,
            nameof(AccelerateTime), 0, 1000);
    }

    public void StopTimer()
    {
        if (timer != null)
        {
            timer.Dispose();
            timer = null;
        }
    }

    public static async ValueTask OnExitRestoreHosts()
    {
        var s = Ioc.Get_Nullable<IHostsFileService>();
        if (s != null)
        {
            var needClear = s.ContainsHostsByTag();
            if (needClear)
            {
                await s.OnExitRestoreHosts();
            }
        }
    }

    public async void FixNetwork()
    {
        await OnExitRestoreHosts();

#if WINDOWS
        {
            await reverseProxyService.StopProxyAsync();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    Arguments = "netsh winsock reset",
                });
            }
            catch
            {
            }
        }
#endif

        Toast.Show(ToastIcon.Success, Strings.FixNetworkComplete);
    }

    public async Task<bool> RefreshIpv6Support()
    {
        var result = await IMicroServiceClient.Instance.Accelerate.GetMyIP(ipV6: true);
        if (!result.IsSuccess) return false;
        IPv6AddresString = result.Content;
        return !string.IsNullOrEmpty(IPv6AddresString);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCoreAsync().ConfigureAwait(false);

        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    async void Dispose(bool disposing)
    {
        if (disposing)
        {
            await ExitAsync().ConfigureAwait(false);
        }
    }

    async ValueTask DisposeAsyncCoreAsync()
    {
        await ExitAsync().ConfigureAwait(false);
    }

    public async ValueTask ExitAsync()
    {
        if (ProxyStatus)
        {
            await StopProxyServiceAsync(isExit: true);
        }
        reverseProxyService.Dispose();
    }
}