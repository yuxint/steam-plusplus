namespace BD.WTTS;

public partial interface IApplication
{
    Dictionary<string, TrayMenuItem>? TrayMenus { get; }

    /// <summary>
    /// 创建或修改托盘菜单
    /// </summary>
    /// <param name="menuKey"></param>
    /// <param name="trayMenuItem"></param>
    void UpdateMenuItems(string menuKey, TrayMenuItem trayMenuItem);

    /// <summary>
    /// 更新菜单栏图标加速状态：加速中正常显色，未加速灰显
    /// </summary>
    /// <param name="isAccelerating"></param>
    void UpdateTrayIconStatus(bool isAccelerating);
}
