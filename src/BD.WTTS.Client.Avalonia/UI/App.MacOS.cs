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

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern void objc_msgSend_ActivateIgnoringOtherApps(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool ignoreOtherApps);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern nint objc_msgSend_Int(nint receiver, nint selector, nint argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern void objc_msgSend_IntVoid(nint receiver, nint selector, nint argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern void objc_msgSend_Release(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern nuint objc_msgSend_Count(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern nint objc_msgSend_Index(nint receiver, nint selector, nuint index);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern nint objc_msgSend_Data(nint receiver, nint selector, byte[] bytes, nuint length);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern void objc_msgSend_SetBool(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [StructLayout(LayoutKind.Sequential)]
    struct CGSize
    {
        public double Width;
        public double Height;

        public CGSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    static extern void objc_msgSend_SetSize(nint receiver, nint selector, CGSize size);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    static extern nint object_getClassName(nint obj);

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
    /// 将应用激活到前台；应用处于后台（如从菜单栏图标恢复窗口）时，不激活应用则窗口不会置前显示
    /// </summary>
    public static void ActivateApp()
    {
        try
        {
            var nsapp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            if (nsapp == nint.Zero)
                return;
            objc_msgSend_ActivateIgnoringOtherApps(nsapp, sel_registerName("activateIgnoringOtherApps:"), true);
        }
        catch (Exception ex)
        {
            Log.Error(nameof(App), ex, "ActivateApp fail.");
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

    /// <summary>已定位到的菜单栏 NSStatusItem 按钮，进程内唯一，找到后缓存</summary>
    static nint? trayStatusBarButton;

    static string GetObjCClassName(nint obj) =>
        obj == nint.Zero ? string.Empty : Marshal.PtrToStringUTF8(object_getClassName(obj)) ?? string.Empty;

    /// <summary>
    /// 查找本应用菜单栏 NSStatusItem 的按钮：遍历本应用窗口找到状态项窗口（NSStatusBarWindow），
    /// 再在其内容视图树中递归定位 NSStatusBarButton（macOS 15 层级：ContentView → NSView → Button）
    /// </summary>
    static nint FindTrayStatusBarButton()
    {
        var nsapp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        if (nsapp == nint.Zero)
            return nint.Zero;
        var windows = objc_msgSend(nsapp, sel_registerName("windows"));
        if (windows == nint.Zero)
            return nint.Zero;
        var count = objc_msgSend_Count(windows, sel_registerName("count"));
        for (nuint i = 0; i < count; i++)
        {
            var window = objc_msgSend_Index(windows, sel_registerName("objectAtIndex:"), i);
            if (GetObjCClassName(window) != "NSStatusBarWindow")
                continue;
            var contentView = objc_msgSend(window, sel_registerName("contentView"));
            if (GetObjCClassName(contentView) == "NSStatusBarButton")
                return contentView;
            return FindStatusBarButtonInSubviews(contentView);
        }
        return nint.Zero;
    }

    static nint FindStatusBarButtonInSubviews(nint view)
    {
        if (view == nint.Zero)
            return nint.Zero;
        var subviews = objc_msgSend(view, sel_registerName("subviews"));
        var count = objc_msgSend_Count(subviews, sel_registerName("count"));
        for (nuint i = 0; i < count; i++)
        {
            var subview = objc_msgSend_Index(subviews, sel_registerName("objectAtIndex:"), i);
            if (GetObjCClassName(subview) == "NSStatusBarButton")
                return subview;
            var nested = FindStatusBarButtonInSubviews(subview);
            if (nested != nint.Zero)
                return nested;
        }
        return nint.Zero;
    }

    /// <summary>
    /// 将 template 图标应用到菜单栏按钮（须在主线程调用）；按钮尚未创建时返回 false，由调用方重试
    /// </summary>
    public static bool ApplyTrayTemplateImage(byte[] png)
    {
        try
        {
            if (trayStatusBarButton is not nint button)
            {
                button = FindTrayStatusBarButton();
                if (button == nint.Zero)
                    return false;
                trayStatusBarButton = button;
            }
            var data = objc_msgSend_Data(objc_getClass("NSData"), sel_registerName("dataWithBytes:length:"),
                png, (nuint)png.Length);
            var image = objc_msgSend_Int(objc_msgSend(objc_getClass("NSImage"), sel_registerName("alloc")),
                sel_registerName("initWithData:"), data);
            if (image == nint.Zero)
                return false;
            objc_msgSend_SetBool(image, sel_registerName("setTemplate:"), true);
            objc_msgSend_SetSize(image, sel_registerName("setSize:"), new CGSize(18, 18));
            objc_msgSend_IntVoid(button, sel_registerName("setImage:"), image);
            objc_msgSend_Release(image, sel_registerName("release"));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(nameof(App), ex, "ApplyTrayTemplateImage fail.");
            return true; // 出错后返回 true 让重试停止，避免无限循环
        }
    }
}
#endif
