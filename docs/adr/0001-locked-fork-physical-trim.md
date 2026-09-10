# ADR 0001: 锁死 fork、物理裁剪为 macOS 纯加速应用，沿用官方身份

日期：2026-09-08
状态：已接受

## 背景

用户基于 WattToolkit（BeyondDimension/SteamTools clone）改造出 macOS 专用应用，四个需求（Cmd+H/M、关闭进后台隐藏 Dock、加速分组白名单三组、启动即加速）已在代码层落码。现进一步要求做成"轻量级"，经拷问确认目标为：物理删除无用代码（非界面隐藏），完全替代并卸载官方 Steam++，不再跟随上游更新。

## 决策

1. **锁死 fork**：不再 merge 上游。删除手术可以激进（物理删工程与文件，不保留配置开关）。
2. **物理裁剪范围**：
   - 删除 6 个非加速插件工程（ASF+、身份验证器、游戏账户、游戏列表、游戏工具、挂机卡片）及孤儿工程 Plugins.Update；XunYouSDK、WebView2 为 Windows-only，macOS 产物本就不含，一并删除源码。
   - 删除 GM 脚本系统（脚本下载/编译/更新、"启用脚本"开关）。
   - 删除非 Hosts 代理模式（系统代理、PAC），仅保留 Hosts 模式。
   - 删除 Steam 进程管理（托盘"启动/关闭 Steam"、SteamConnectService、Facepunch.Steamworks 依赖）。
   - 删除线上功能：登录账户、首页广告、公告、AppCenter 统计上报、自动更新（更新删除是硬要求：防改造版被官方更新覆盖）。
   - 主窗口只保留加速页 + 设置页（首页 tab 删除）。
3. **沿用官方身份**：bundle id `net.steampp.app`、数据目录 `~/Library/Steam++` 不变。卸载官方版后零配置继承加速勾选、已信任根证书、含 Google 翻译组的本地缓存。改造版与官方版不可并存。
4. **基线 = tag 3.1.0**（commit 8612251，2026-03-11 正式版）：锁定在该发布版而非 develop 顶端，理由：SDK 零额外安装（本机 .NET 10.0.400 恰好匹配，3.1.0 无 global.json 锁版本）、避开 develop 上 .NET 11 预览与 Avalonia 12 回滚的试验窗口、与用户实际使用的官方版本一致。develop 上唯一值得取的提交 `048fb345e`（安全修复：特权路径任意文件覆盖，`-clt ayaneo` 命令）已作为工作区改动应用于基线。3.1.0 与 develop 的加速器代码差异仅 8 行，本地缓存格式兼容。
5. **保留不动**：加速核心链路（微服务客户端拉分组 + 本地缓存合并、Yarp 反向代理子进程、hosts 提权写入、菜单栏加速启停）、四需求窗口行为（需在 3.1.0 基线上重做，原 develop 基线的改动存于 stash 与 `.four-features-on-head.patch`）。
6. **实施顺序**：先建编译基线（`dotnet workload install macos` + 子模块对齐 3.1.0 指针、跑通一次未裁剪的 macOS 构建），再动刀。禁止无基线裁剪。

## 后果

- 体积收益有限（插件代码约 10-20MB；大头是 .NET 运行时、双架构 Yarp 单文件、Avalonia/Skia 原生库），"轻量"的主要收益在启动速度、内存、维护面。
- 主界面 tab 与设置页分组由插件列表自动派生，删工程即自动消失，无需逐页手改。
- 未来若官方删光白名单分组，本地缓存合并机制是唯一防线，不可移除。
- 官方版必须先卸载再装改造版，避免数据目录互踩。

## 实施进度

- 2026-09-08：基线编译跑通；四需求在 3.1.0 基线重做完成。
- 2026-09-09：P1 物理裁剪（6 非加速插件、Plugins.Update、WebView2、XunYouSDK、Steam 进程管理）完成；P2 GM 脚本系统删除完成；P3 非 Hosts 代理模式（系统代理/PAC）删除完成。
- 2026-09-09：P4 线上功能删除完成——自动更新（IAppUpdateService 全链 + 设置键/设置项）、AppCenter 统计上报（含子进程挂钩，TracepointHelper 保留空实现供插件调用）、ActiveUser 启动上报、首页广告与公告（服务 + AdControl + NoticeFlyout + 设置项）、首页/插件商店 tab（主窗口仅剩插件加速页 + 设置页，About 页随之删除"检查更新/复制 UID/账号注销"链接）；登录 UI 入口（标题栏用户菜单）删除。登录账户栈（UserService/LoginOrRegisterWindowViewModel/ThirdPartyLoginHelper）因插件内 XunYou 游戏加速（Windows-only，待后续裁剪）仍被引用而保留，macOS 上已无任何登录入口。顺带清理 resx 中脚本与 SystemProxy 死键、AcceleratorPage(2).axaml 中"加速模式/启用脚本"等注释块。三端编译通过，无头探针 304 类型全过，被删类型在 MonoBundle 加载域中确认不存在。
