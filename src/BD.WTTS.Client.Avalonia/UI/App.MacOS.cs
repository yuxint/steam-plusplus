#if MACOS
using System.Runtime.InteropServices;

namespace BD.WTTS.UI;

partial class App
{
    /// <summary>
    /// NSApplicationActivationPolicy.Regular，常规应用，显示 Dock 栏图标
    /// </summary>
    const nint NSApplicationActivationPolicyRegular = 0;

    /// <summary>
    /// NSApplicationActivationPolicy.Accessory，附属应用，不显示 Dock 栏图标
    /// </summary>
    const nint NSApplicationActivationPolicyAccessory = 1;

    [DllImport("/usr/lib/libobjc.A.dylib")]
    static extern nint objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    static extern nint sel_registerName(string selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern nint objc_msgSend_SetActivationPolicy(nint receiver, nint selector, nint policy);

    /// <summary>
    /// 设置 Dock 栏图标是否显示，隐藏后应用保留菜单栏图标继续后台运行
    /// </summary>
    public static void SetDockIconVisible(bool visible)
    {
        try
        {
            var nsapp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            if (nsapp == nint.Zero)
                return;
            objc_msgSend_SetActivationPolicy(nsapp, sel_registerName("setActivationPolicy:"),
                visible ? NSApplicationActivationPolicyRegular : NSApplicationActivationPolicyAccessory);
        }
        catch (Exception ex)
        {
            Log.Error(nameof(App), ex, "SetDockIconVisible fail.");
        }
    }

    /// <summary>
    /// 隐藏主窗口并从 Dock 栏消失，仅保留菜单栏图标，可从菜单栏退出
    /// </summary>
    public static void HideMainWindowToBackground()
    {
        try
        {
            Instance.MainWindow?.Hide();
            SetDockIconVisible(false);
        }
        catch (Exception ex)
        {
            Log.Error(nameof(App), ex, "HideMainWindowToBackground fail.");
        }
    }

    /// <summary>
    /// 最小化主窗口到 Dock 栏
    /// </summary>
    public static void MinimizeMainWindow()
    {
        var mainWindow = Instance.MainWindow;
        if (mainWindow != null)
            mainWindow.WindowState = WindowState.Minimized;
    }
}
#endif
