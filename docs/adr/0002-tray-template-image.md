# ADR 0002: 菜单栏图标采用 NSStatusItem template image + 原生互操作

日期：2026-09-17
状态：已接受

## 背景

菜单栏图标原先只有一张彩色 `ApplicationIcon.ico`，无法直观体现加速状态；「启动/停止」藏在「网络加速」二级子菜单且不随状态变化。经评审确认三需求：未加速时图标灰显、启停提升为一级、合并为单个随状态切换的菜单项。

灰显方案有两条路：彩色 logo 降透明度两态（实现最简，但彩色图标挂菜单栏不合 macOS 惯例，且非 template 图标在深色菜单栏下观感不可控）；或 macOS 标准单色 template image（系统按菜单栏深浅自动反色，灰显 = 降 alpha）。选定 template 方案，与既有的极简苹果风 UI 方向一致。

障碍：Avalonia 11.3 的 `TrayIcon` 不支持 template image（`SetIcon` 只从流创建普通 NSImage，无任何 template 接口），且 NSStatusItem 由 Avalonia.Native 原生层持有，托管侧拿不到句柄。

## 决策

1. **保留 Avalonia `TrayIcon`** 继续负责菜单（NativeMenu）与生命周期，不重造 NSStatusItem。
2. **原生互操作替换图标**：沿用 `App.MacOS.cs` 的 `objc_msgSend` P/Invoke 模式，遍历 `[NSApplication windows]` 找到 `NSStatusBarWindow`，在其内容视图树中递归定位 `NSStatusBarButton`，用 `setImage:` 替换为自建 NSImage（`NSData dataWithBytes:length:` 创建 → `setTemplate: YES` → `setSize: 18×18`）。
3. **图标资源两态**：由彩色 logo（512px）降采样生成黑 + alpha 的 36×36 PNG（18pt @2x，内容 32px 居中留边）：`TrayAccelerateOn.png`（原始 alpha，加速中=正常显色）、`TrayAccelerateOff.png`（alpha×0.35，未加速=灰显）。
4. **状态驱动**：加速插件 `ProxyService` 订阅 `ProxyStatus`/`ProxyStarting`（`Skip(1)` 跳过订阅初值），变化时重建托盘菜单（文案：启动加速 / 停止加速 / 正在启动加速…+ 过渡期禁用）并调用 `IApplication.UpdateTrayIconStatus` 切图标。启动时由 `DispatcherTimer` 重试（300ms×20）应用图标，规避 NSStatusItem 尚未创建的时序问题。
5. 新增资源串 `CommunityFix_StartAccelerate`（启动加速 / 啟動加速 / Start routing）。

## 后果

- 图标随系统深浅色菜单栏自动反色，无需自行监听外观变化；template 的灰显语义由 alpha 表达。
- 按钮定位依赖 AppKit 私有视图层级。macOS 15 实测为 `NSStatusBarWindow → NSStatusBarContentView → NSView → NSStatusBarButton`（按钮嵌套两层，故必须递归查找，单层扫描找不到）。系统大版本升级若层级再变，症状是图标退回彩色 logo（回退安全，不崩溃），需更新 `FindTrayStatusBarButton` 的递归查找。
- Avalonia 升级若原生支持 template image 或暴露 NSStatusItem 句柄，可删除这段互操作回归纯托管实现。
- 每次状态切换都重建整个 NativeMenu（既有 `UpdateMenuItems` 的机制），频率低（仅状态变化），可接受。
- 应用整体退出时子进程/图标回收逻辑不变。

## 实施进度

- 2026-09-17：实现完成并装机验证。目检矩阵：加速中=图标实心亮白+菜单「停止加速」+ hosts 写入 45 条 + 443 监听；未加速=图标灰显+菜单「启动加速」+ hosts 还原 + 443 关闭。启动过渡期文案「正在启动加速...」并禁用（代码路径，未单独目检）。
- 已知边界：停止加速后 `Steam++.Accelerator` 子进程热待命不退出（既有行为，复用验证一致），不影响状态判定。
