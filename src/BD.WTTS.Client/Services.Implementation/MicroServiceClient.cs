using HttpVersion = System.Net.HttpVersion;

namespace BD.WTTS.Services.Implementation;

sealed class MicroServiceClient : MicroServiceClientBase
{
    internal const string ClientName = ClientName_;

    public MicroServiceClient(
        ILoggerFactory loggerFactory,
        IHttpClientFactory clientFactory,
        IHttpPlatformHelperService httpPlatformHelper,
        IToast toast,
        IOptions<AppSettings> options,
        IModelValidator validator,
        IApplicationVersionService appVerService) : base(
            loggerFactory.CreateLogger<MicroServiceClient>(),
            loggerFactory,
            clientFactory,
            httpPlatformHelper,
            toast,
            NullAuthHelper.Instance,
            options.Value,
            validator,
            appVerService)
    {
    }

    protected sealed override HttpClient CreateClient(HttpHandlerCategory category)
    {
        category = HttpHandlerCategory.Default;
        var client = base.CreateClient(category);

        try
        {
            client.BaseAddress = new Uri(ApiBaseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
#if NETCOREAPP3_0_OR_GREATER
            client.DefaultRequestVersion = HttpVersion.Version20;
#endif
        }
        catch (InvalidOperationException)
        {

        }

        return client;
    }

    protected sealed override void SetDeviceId(IDeviceId deviceId)
    {
        var deviceIdG = DeviceIdHelper.DeviceIdG;
        var deviceIdR = DeviceIdHelper.DeviceIdR;
        var deviceIdN = DeviceIdHelper.DeviceIdN;

        deviceId.DeviceIdG = deviceIdG;
        deviceId.DeviceIdR = deviceIdR;
        deviceId.DeviceIdN = deviceIdN;
    }

    /// <summary>
    /// 无登录态：改造版已裁掉个人中心，接口一律匿名调用
    /// </summary>
    sealed class NullAuthHelper : IAuthHelper
    {
        public static readonly NullAuthHelper Instance = new();

        ValueTask<JWTEntity?> IAuthHelper.GetAuthTokenAsync() => ValueTask.FromResult<JWTEntity?>(default);

        ValueTask<JWTEntity?> IAuthHelper.GetShopAuthTokenAsync() => ValueTask.FromResult<JWTEntity?>(default);

        Task IAuthHelper.SignOutAsync() => Task.CompletedTask;
    }
}
