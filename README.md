<h1 align="center">Watt Toolkit macOS 改造版</h1>

<div align="center">

[English](./README.en.md) | 简体中文

基于开源项目 [Watt Toolkit（原名 Steam++）](https://github.com/BeyondDimension/SteamTools) 锁定 fork 后深度裁剪的 **macOS（Apple Silicon）专用轻量版**，只保留「网络加速」一个功能域。

**本项目为非官方改造版，与 Watt Toolkit 官方（Beyond Dimension）无关**，请勿将本项目的问题反馈到官方渠道。

</div>

## 项目定位

- 目标是替代官方 Steam++ 在 macOS 上的加速功能，去掉官方版中与加速无关的功能域和跨平台代码，做成体积更小的 macos-lite 版本。
- 采用「锁定 fork + 物理裁剪」的方式改造，决策记录见 [docs/adr/0001-locked-fork-physical-trim.md](./docs/adr/0001-locked-fork-physical-trim.md)，项目术语表见 [CONTEXT.md](./CONTEXT.md)。

## 功能范围

- 仅保留网络加速，且加速分组限定白名单：**公共 CDN、Google 翻译、Github**，白名单之外的分组不进入界面。
- 唯一加速模式为 **Hosts 模式**：改写系统 hosts 将目标域名指向本地 Yarp 反向代理子进程，配合本地根证书做转发。系统代理与 PAC 模式不属于本改造版。
- 菜单栏常驻图标是加速启停与退出的唯一入口；支持「启动即加速」；关闭主窗口后进入后台隐藏态，加速继续运行。

## 与官方版的关系

- 沿用官方身份：bundle id `net.steampp.app`、数据目录 `~/Library/Steam++`，因此**与官方版不可并存**。
- 卸载官方版后安装本改造版，可零配置继承既有数据（加速勾选、已信任根证书、加速分组缓存、启动配置）。

## 构建与运行

环境要求：macOS（Apple Silicon）、Xcode、.NET SDK（含 `net10.0-macos` 工作负载）。

```bash
git clone --recursive https://github.com/yuxint/steam-plusplus.git
cd steam-plusplus
./build/macos/build.sh
```

- 产物为 `src/BD.WTTS.Client.Avalonia.App/bin/Release/net10.0-macos/osx-arm64/Steam++.app`。
- 完整编译日志默认写入 `/tmp/steampp-macos-build.log`（可用 `LOG_FILE` 覆盖）。
- 可选代码签名：`CODESIGN_IDENTITY="Apple Development: ..." ./build/macos/build.sh`（默认不签名）。

## 开源许可

- 本项目遵循上游的 [GNU GPL-3.0](./LICENSE) 许可证开源。
- 本项目基于 [Watt Toolkit](https://github.com/BeyondDimension/SteamTools)（GPL-3.0）修改，与上游相同处以 GPL-3.0 发布。
- Watt Toolkit、Steam++ 名称及相关品牌权益归原作者所有，本项目仅作为衍生修改版标识来源之用。
