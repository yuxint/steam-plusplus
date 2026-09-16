#if MACOS || MACCATALYST || IOS
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable once CheckNamespace
namespace BD.WTTS.Services.Implementation;

partial class MacCatalystPlatformServiceImpl
{
    const string LibObjC = "/usr/lib/libobjc.A.dylib";
    const string LibCoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const string LibCoreServices = "/System/Library/Frameworks/CoreServices.framework/CoreServices";

    const int kCFStringEncodingUTF8 = 0x08000100;
    const nint kCFURLPOSIXPathStyle = 0;
    const uint kLSSharedFileListResolutionFlags = 1 | 4; // kLSSharedFileListNoUserInteraction | kLSSharedFileListDoNotMountVolumes

    /// <summary>
    /// kLSSharedFileListSessionLoginItems 的类型值，对应系统设置中"登录时打开"列表
    /// </summary>
    const string LoginItemsListType = "com.apple.LSSharedFileList.LoginItems";

    #region Native

    [DllImport(LibObjC)]
    static extern nint objc_getClass(string name);

    [DllImport(LibObjC)]
    static extern nint sel_registerName(string selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool objc_msgSend_RegisterWithError(nint receiver, nint selector, nint error);

    [DllImport(LibCoreFoundation)]
    static extern nint CFStringCreateWithCString(nint allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string str, int encoding);

    [DllImport(LibCoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool CFStringGetCString(nint str, byte[] buffer, nint bufferSize, int encoding);

    [DllImport(LibCoreFoundation)]
    static extern nint CFURLCreateWithFileSystemPath(nint allocator, nint path, nint pathStyle, [MarshalAs(UnmanagedType.U1)] bool isDirectory);

    [DllImport(LibCoreFoundation)]
    static extern nint CFURLCopyFileSystemPath(nint url, nint pathStyle);

    [DllImport(LibCoreFoundation)]
    static extern nint CFArrayGetCount(nint array);

    [DllImport(LibCoreFoundation)]
    static extern nint CFArrayGetValueAtIndex(nint array, nint index);

    [DllImport(LibCoreFoundation)]
    static extern void CFRelease(nint cf);

    [DllImport(LibCoreServices)]
    static extern nint LSSharedFileListCreate(nint allocator, nint listType, nint listOptions);

    [DllImport(LibCoreServices)]
    static extern nint LSSharedFileListCopySnapshot(nint inList, out uint outSnapshotSeed);

    [DllImport(LibCoreServices)]
    static extern nint LSSharedFileListInsertItemURL(nint inList, nint inURL, nint inDisplayName, nint inIconRef, nint inPropertiesToSet, nint inPropertiesToClear);

    [DllImport(LibCoreServices)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool LSSharedFileListItemRemove(nint inList, nint inItem);

    [DllImport(LibCoreServices)]
    static extern nint LSSharedFileListItemCopyResolvedURL(nint inItem, uint inFlags, nint outError);

    #endregion

    /// <inheritdoc cref="IPlatformService.SetBootAutoStart(bool, string)"/>
    void IPlatformService.SetBootAutoStart(bool isAutoStart, string name)
    {
        try
        {
            if (isAutoStart)
            {
                if (SetBootAutoStartBySMAppService(isAutoStart))
                    return;
                // SMAppService 注册失败（如应用未签名），回退到传统登录项，同样显示在系统设置的"登录时打开"中
                SetBootAutoStartBySharedFileList(isAutoStart);
            }
            else
            {
                // 不清楚此前经由哪种方式注册，两种渠道都尝试移除
                SetBootAutoStartBySMAppService(isAutoStart);
                SetBootAutoStartBySharedFileList(isAutoStart);
            }
        }
        catch (Exception e)
        {
            e.LogAndShowT(TAG, msg: $"SetBootAutoStart fail, isAutoStart: {isAutoStart}.");
        }
    }

    /// <summary>
    /// 通过 SMAppService 注册主应用为登录项（macOS 13+）
    /// </summary>
    /// <returns>注册成功或已在系统设置中等待用户确认时返回 <see langword="true"/></returns>
    static bool SetBootAutoStartBySMAppService(bool isAutoStart)
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(13))
            return false;
        var cls = objc_getClass("SMAppService");
        if (cls == nint.Zero)
            return false;
        var service = objc_msgSend(cls, sel_registerName("mainAppService"));
        if (service == nint.Zero)
            return false;
        if (isAutoStart)
        {
            if (objc_msgSend_RegisterWithError(service, sel_registerName("registerAndReturnError:"), nint.Zero))
                return true;
            var status = objc_msgSend(service, sel_registerName("status"));
            // status: 0 未注册、1 已启用、2 等待用户在系统设置中确认、404 未找到应用
            if (status == 1 || status == 2)
                return true;
            Log.Error(TAG, $"SMAppService register fail, status: {status}.");
            return false;
        }
        else
        {
            // 本就未注册时注销会失败，忽略即可
            objc_msgSend_RegisterWithError(service, sel_registerName("unregisterAndReturnError:"), nint.Zero);
            return true;
        }
    }

    /// <summary>
    /// 通过 LSSharedFileList（传统登录项 API）注册/移除，系统设置中同样显示在"登录时打开"中
    /// </summary>
    static void SetBootAutoStartBySharedFileList(bool isAutoStart)
    {
        var appPath = GetAppBundlePath();
        if (appPath == null)
        {
            Log.Error(TAG, "SetBootAutoStartBySharedFileList fail, app bundle not found.");
            return;
        }
        var listType = CFStringCreateWithCString(nint.Zero, LoginItemsListType, kCFStringEncodingUTF8);
        if (listType == nint.Zero)
            return;
        var list = LSSharedFileListCreate(nint.Zero, listType, nint.Zero);
        CFRelease(listType);
        if (list == nint.Zero)
        {
            Log.Error(TAG, "SetBootAutoStartBySharedFileList fail, LSSharedFileListCreate return null.");
            return;
        }
        try
        {
            if (isAutoStart)
            {
                var pathRef = CFStringCreateWithCString(nint.Zero, appPath, kCFStringEncodingUTF8);
                if (pathRef == nint.Zero)
                    return;
                var url = CFURLCreateWithFileSystemPath(nint.Zero, pathRef, kCFURLPOSIXPathStyle, true);
                CFRelease(pathRef);
                if (url == nint.Zero)
                {
                    Log.Error(TAG, "SetBootAutoStartBySharedFileList fail, create url fail.");
                    return;
                }
                try
                {
                    var item = LSSharedFileListInsertItemURL(list, url, nint.Zero, nint.Zero, nint.Zero, nint.Zero);
                    if (item != nint.Zero)
                        CFRelease(item);
                    else
                        Log.Error(TAG, "SetBootAutoStartBySharedFileList fail, insert item fail.");
                }
                finally
                {
                    CFRelease(url);
                }
            }
            else
            {
                var snapshot = LSSharedFileListCopySnapshot(list, out _);
                if (snapshot == nint.Zero)
                    return;
                try
                {
                    var count = CFArrayGetCount(snapshot);
                    for (nint i = 0; i < count; i++)
                    {
                        var item = CFArrayGetValueAtIndex(snapshot, i);
                        if (item == nint.Zero)
                            continue;
                        var url = LSSharedFileListItemCopyResolvedURL(item, kLSSharedFileListResolutionFlags, nint.Zero);
                        if (url == nint.Zero)
                            continue;
                        try
                        {
                            var itemPath = CFUrlCopyFileSystemPath(url);
                            if (itemPath != null && string.Equals(itemPath, appPath, StringComparison.OrdinalIgnoreCase))
                            {
                                LSSharedFileListItemRemove(list, item);
                                break;
                            }
                        }
                        finally
                        {
                            CFRelease(url);
                        }
                    }
                }
                finally
                {
                    CFRelease(snapshot);
                }
            }
        }
        finally
        {
            CFRelease(list);
        }
    }

    static string? CFUrlCopyFileSystemPath(nint url)
    {
        if (url == nint.Zero)
            return null;
        var pathRef = CFURLCopyFileSystemPath(url, kCFURLPOSIXPathStyle);
        if (pathRef == nint.Zero)
            return null;
        try
        {
            var buffer = new byte[4096];
            if (!CFStringGetCString(pathRef, buffer, buffer.Length, kCFStringEncodingUTF8))
                return null;
            var length = Array.IndexOf(buffer, (byte)0);
            return length < 0 ? null : Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            CFRelease(pathRef);
        }
    }

    /// <summary>
    /// 获取当前 .app 包路径，未打包运行（如 dotnet run）时返回 <see langword="null"/>
    /// </summary>
    static string? GetAppBundlePath()
    {
        // bundleURL 返回的是非持有引用，不可 CFRelease
        var bundle = objc_msgSend(objc_getClass("NSBundle"), sel_registerName("mainBundle"));
        if (bundle != nint.Zero)
        {
            var url = objc_msgSend(bundle, sel_registerName("bundleURL"));
            var path = CFUrlCopyFileSystemPath(url);
            if (path != null && path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return path;
        }
        var processPath = Environment.ProcessPath;
        if (processPath != null)
        {
            const string marker = ".app/Contents/";
            var index = processPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
                return processPath.Substring(0, index + ".app".Length);
        }
        return null;
    }
}
#endif
