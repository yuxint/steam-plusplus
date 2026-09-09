// https://github.com/dotnetcore/FastGithub/blob/2.1.4/FastGithub.HttpServer/HttpReverseProxyMiddleware.cs

using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;
using HttpVersion = System.Net.HttpVersion;

// ReSharper disable once CheckNamespace
namespace BD.WTTS.Services.Implementation;

/// <summary>
/// 反向代理中间件
/// </summary>
sealed class HttpReverseProxyMiddleware
{
    static readonly IDomainConfig defaultDomainConfig = new DomainConfig() { TlsSni = true, };

    readonly IHttpForwarder httpForwarder;
    readonly IReverseProxyHttpClientFactory httpClientFactory;
    readonly IReverseProxyConfig reverseProxyConfig;
    readonly ILogger<HttpReverseProxyMiddleware> logger;

    public HttpReverseProxyMiddleware(
        IHttpForwarder httpForwarder,
        IReverseProxyHttpClientFactory httpClientFactory,
        IReverseProxyConfig reverseProxyConfig,
        ILogger<HttpReverseProxyMiddleware> logger)
    {
        this.httpForwarder = httpForwarder;
        this.httpClientFactory = httpClientFactory;
        this.reverseProxyConfig = reverseProxyConfig;
        this.logger = logger;
    }

    static ArgumentOutOfRangeException GetUnknownHttpVersionException(string? actualValue, [CallerArgumentExpression(nameof(actualValue))] string? paramName = null) => new(
$"""
Version doesn't map to a known HTTP protocol. (Parameter '{paramName}')
Actual value was {actualValue}.
""");

    static Version GetHttpVersion(string requestProtocol) =>
    (requestProtocol != null && requestProtocol.Length >= 6) ? requestProtocol[5] switch
    {
        // 参考 Microsoft.AspNetCore.Http.HttpProtocol.GetHttpProtocol
        '1' => requestProtocol.Length >= 8 ? requestProtocol[7] switch
        {
            '0' => HttpVersion.Version10,
            '1' => HttpVersion.Version11,
            _ => throw GetUnknownHttpVersionException(requestProtocol),
        } : throw GetUnknownHttpVersionException(requestProtocol),
        '2' => HttpVersion.Version20,
        '3' => HttpVersion.Version30,
        _ => throw GetUnknownHttpVersionException(requestProtocol),
    } : throw GetUnknownHttpVersionException(requestProtocol);

    /// <summary>
    /// 处理请求
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var url = context.Request.GetDisplayUrl();
        //var url = context.Request.GetDisplayUrl().Remove(0, context.Request.Scheme.Length + 3);

        if (TryGetDomainConfig(url.Remove(0, context.Request.Scheme.Length + 3), out var domainConfig) == false)
        {
            if (reverseProxyConfig.Service.TwoLevelAgentEnable)
            {
                var httpClient = httpClientFactory.CreateHttpClient("GlobalProxy", defaultDomainConfig);
                var destinationPrefix = GetDestinationPrefix(context.Request.Scheme, context.Request.Host, null);
                var forwarderRequestConfig = new ForwarderRequestConfig()
                {
                    Version = GetHttpVersion(context.Request.Protocol),
                };
                var error = await httpForwarder.SendAsync(context, destinationPrefix, httpClient, forwarderRequestConfig, HttpTransformer.Empty);
                if (error != ForwarderError.None)
                {
                    await HandleErrorAsync(context, error);
                }
            }
            else
            {
                await next(context);
            }
            return;
        }

        if (domainConfig == defaultDomainConfig)
        {
            // 部分运营商将奇怪的域名解析到 127.0.0.1 再此排除这些不支持的代理域名
            var ip = await reverseProxyConfig.DnsAnalysis.AnalysisDomainIpAsync(context.Request.Host.Value!, IDnsAnalysisService.DNS_Dnspods).FirstOrDefaultAsync();
            if (ip == null || IPAddress.IsLoopback(ip))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync($"域名 {context.Request.Host.Host} 可能已经被 DNS 污染，如果域名为本机域名，请解析为非回环 IP。", Encoding.UTF8);
                return;
            }
        }

        if (domainConfig.Items.Any_Nullable())
            domainConfig = RecursionMatchDomainConfig(url, domainConfig);

        if (domainConfig.Response == null)
        {
            if (reverseProxyConfig.Service.EnableHttpProxyToHttps && context.Request.Scheme == Uri.UriSchemeHttp)
            {
                context.Response.Redirect(Uri.UriSchemeHttps + "://" + context.Request.Host.Host + context.Request.RawUrl());
                return;
            }

            var destination = domainConfig.Destination;
            if (domainConfig.Destination?.AbsoluteUri.Contains("@") == true)
            {
                var newUrl = domainConfig.Destination.AbsoluteUri.Replace("@domain", context.Request.Host.Host);
                newUrl = newUrl.Replace("@uri", context.Request.RawUrl());
                destination = new Uri(newUrl);
            }

            var destinationPrefix = GetDestinationPrefix(context.Request.Scheme, context.Request.Host, destination);
            var httpClient = httpClientFactory.CreateHttpClient(context.Request.Host.Host, domainConfig);
            if (!string.IsNullOrEmpty(domainConfig.UserAgent))
            {
                context.Request.Headers.UserAgent = domainConfig.UserAgent.Replace("${origin}", context.Request.Headers.UserAgent, StringComparison.OrdinalIgnoreCase);
            }

            ForwarderRequestConfig forwarderRequestConfig;

            if (domainConfig.IsServerSideProxy)
            {
                SetWattHeaders(context, reverseProxyConfig.Service.ServerSideProxyToken);
                forwarderRequestConfig = new ForwarderRequestConfig()
                {
                    Version = HttpVersion.Version30,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                };
            }
            else
            {
                forwarderRequestConfig = new ForwarderRequestConfig()
                {
                    Version = GetHttpVersion(context.Request.Protocol),
                };
            }

            var error = await httpForwarder.SendAsync(context, destinationPrefix, httpClient, forwarderRequestConfig, HttpTransformer.Empty);

            if (error != ForwarderError.None)
            {
                await HandleErrorAsync(context, error);
            }
        }
        else
        {
            context.Response.StatusCode = (int)domainConfig.Response.StatusCode;
            context.Response.ContentType = domainConfig.Response.ContentType;
            if (domainConfig.Response.ContentValue != null)
            {
                await context.Response.WriteAsync(domainConfig.Response.ContentValue);
            }
        }
    }

    /// <summary>
    /// 递归匹配子域名配置
    /// </summary>
    /// <param name="url"></param>
    /// <param name="domainConfig"></param>
    /// <returns></returns>
    static IDomainConfig RecursionMatchDomainConfig(string url, IDomainConfig domainConfig)
    {
        if (domainConfig.Items.Any_Nullable())
        {
            var item = domainConfig.Items.FirstOrDefault(s => s.Key.IsMatch(url)).Value;
            if (item != null)
                return RecursionMatchDomainConfig(url, item);
        }
        return domainConfig;
    }

    bool TryGetDomainConfig(string uri, [MaybeNullWhen(false)] out IDomainConfig domainConfig)
    {
        domainConfig = null;

        if (reverseProxyConfig.TryGetDomainConfig(uri, out domainConfig) == true)
        {
            return true;
        }

        var host = new UriBuilder(uri).Host;
        // 未配置的域名，但仍然被解析到本机 IP 的域名
        if (IsDomain(host))
        {
            logger.LogWarning(
                "域名 {host} 可能已经被 DNS 污染，如果域名为本机域名，请解析为非回环 IP。", host);
            domainConfig = defaultDomainConfig;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 是否为域名
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    static bool IsDomain(string host) => !IPAddress.TryParse(host, out _) && host.Contains('.');

    /// <summary>
    /// 获取目标前缀
    /// </summary>
    /// <param name="scheme"></param>
    /// <param name="host"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    string GetDestinationPrefix(string scheme, HostString host, Uri? destination)
    {
        var defaultValue = $"{scheme}://{host}/";
        if (destination == null)
        {
            return defaultValue;
        }

        var baseUri = new Uri(defaultValue);
        var result = new Uri(baseUri, destination).ToString();
        logger.LogInformation("{defaultValue} => {result}", defaultValue, result);
        return result;
    }

    /// <summary>
    /// 处理错误信息
    /// </summary>
    /// <param name="context"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    static async Task HandleErrorAsync(HttpContext context, ForwarderError error)
    {
        await context.Response.WriteAsync($"{error}:{context.GetForwarderErrorFeature()?.Exception?.Message}");
    }

    static void SetWattHeaders(HttpContext context, string? token)
    {
        context.Request.Headers.TryAdd("X-Watt-Origin-Dest-Scheme", context.Request.Scheme);
        context.Request.Headers.TryAdd("X-Watt-Origin-Dest-Host", context.Request.Host.ToString());
        context.Request.Headers.TryAdd("X-Watt-Origin-Dest-PathAndQuery", context.Request.GetEncodedPathAndQuery());

        context.Request.Headers.TryAdd("X-Watt-Token", token ?? string.Empty);
    }
}