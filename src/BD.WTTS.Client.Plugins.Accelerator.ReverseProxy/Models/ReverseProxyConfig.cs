// https://github.com/dotnetcore/FastGithub/blob/2.1.4/FastGithub.Configuration/FastGithubConfig.cs

// ReSharper disable once CheckNamespace
namespace BD.WTTS.Models;

sealed class ReverseProxyConfig : IReverseProxyConfig
{
    readonly SortedDictionary<DomainPattern, IDomainConfig> domainConfigs;
    readonly ConcurrentDictionary<string, IDomainConfig?> domainConfigCache;
    readonly YarpReverseProxyServiceImpl reverseProxyService;

    public ReverseProxyConfig(YarpReverseProxyServiceImpl reverseProxyService)
    {
        this.reverseProxyService = reverseProxyService;
        domainConfigs = new();
        AddDomainConfigs(domainConfigs, reverseProxyService.ProxyDomains);
        domainConfigCache = new();
    }

    YarpReverseProxyServiceImpl IReverseProxyConfig.Service => reverseProxyService;

    public ushort HttpProxyPort
    {
        get => reverseProxyService.ProxyPort;
        set => reverseProxyService.ProxyPort = value;
    }

    public IReadOnlyCollection<AccelerateProjectDTO>? ProxyDomains
    {
        get => reverseProxyService.ProxyDomains;
        set => reverseProxyService.ProxyDomains = value;
    }

    static void AddDomainConfigs(IDictionary<DomainPattern, IDomainConfig> dict,
        IEnumerable<AccelerateProjectDTO>? accelerates)
    {
        if (accelerates != null)
        {
            foreach (var item in accelerates)
            {
                var matchDomainNames = item.MatchDomainNames;
                if (string.IsNullOrWhiteSpace(matchDomainNames))
                    throw new ArgumentNullException(nameof(matchDomainNames));
                dict.Add(new DomainPattern(matchDomainNames) { Order = item.Order }, item);
            }
        }
    }

    //static void AddDomainConfigs(IDictionary<DomainPattern, IDomainConfig> dict, IReadOnlyDictionary<string, DomainConfig>? domainConfigs)
    //{
    //    if (domainConfigs != null)
    //    {
    //        foreach (var kv in domainConfigs)
    //        {
    //            dict.Add(new DomainPattern(kv.Key), kv.Value);
    //        }
    //    }
    //}

    ///// <summary>
    ///// 配置转换
    ///// </summary>
    ///// <param name="domainConfigs"></param>
    ///// <returns></returns>
    //[Obsolete("use AddDomainConfigs")]
    //static SortedDictionary<DomainPattern, IDomainConfig> ConvertDomainConfigs(IEnumerable<AccelerateProjectDTO>? domainConfigs)
    //{
    //    var result = new SortedDictionary<DomainPattern, IDomainConfig>();
    //    if (domainConfigs != null)
    //    {
    //        foreach (var item in domainConfigs)
    //        {
    //            foreach (var domainName in item.DomainNamesArray)
    //            {
    //                result.Add(new DomainPattern(domainName), item);
    //            }
    //        }
    //    }
    //    return result;
    //}

    public bool TryGetDomainConfig(string url, [MaybeNullWhen(false)] out IDomainConfig value)
    {
        //value = domainConfigCache.GetOrAdd(domain.Host, GetDomainConfig);

        var uri = new UriBuilder(url).Uri;

        domainConfigCache.TryGetValue(uri.Host, out value);
        if (value != null)
            return true;

        value = GetDomainConfig(url);
        if (value == null)
            return false;

        domainConfigCache.TryAdd(uri.Host, value);
        return true;

        IDomainConfig? GetDomainConfig(string url)
        {
            var key = domainConfigs.Keys.FirstOrDefault(item => item.IsMatch(url));
            return key == null ? null : domainConfigs[key];
        }
    }

    public DomainPattern[] GetDomainPatterns() => domainConfigs.Keys.ToArray();
}
