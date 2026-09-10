// ReSharper disable once CheckNamespace
namespace BD.WTTS;

partial class Startup // OnStartup
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShowSettingsModifiedRestartThisSoft()
    {
        if (Ioc.Get_Nullable<IToastIntercept>() is StartupToastIntercept intercept
            && !intercept.IsStartuped)
        {
            return;
        }
        Toast.Show(ToastIcon.Info, Strings.SettingsModifiedRestartThisSoft);
    }

    public virtual void InitSettingSubscribe()
    {
        var a = IApplication.Instance;

        UISettings.Theme.Subscribe(x => a.Theme = x);
        UISettings.Language.Subscribe(ResourceService.ChangeLanguage);

        GeneralSettings.GPU.Subscribe(x =>
        {
            //if (x.HasValue) // null 为默认值时不提示
            ShowSettingsModifiedRestartThisSoft();
        });
        GeneralSettings.PluginSafeMode.Subscribe(x =>
        {
            //if (x.HasValue) // null 为默认值时不提示
            ShowSettingsModifiedRestartThisSoft();
        });
    }

    public virtual void OnStartup()
    {
        StartupToastIntercept.OnStartuped();

#if STARTUP_WATCH_TRACE || DEBUG
        WatchTrace.Start();
#endif
#if DEBUG
        DebugConsole.WriteInfo();
#endif

#if STARTUP_WATCH_TRACE || DEBUG
        WatchTrace.Stop();
#endif
    }
}