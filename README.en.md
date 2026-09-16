<h1 align="center">Watt Toolkit macOS Lite (Modified Edition)</h1>

<div align="center">

English | [简体中文](./README.md)

A **macOS (Apple Silicon) only, heavily trimmed edition** of the open source project [Watt Toolkit (formerly Steam++)](https://github.com/BeyondDimension/SteamTools), locked-forked and stripped down to keep only the network acceleration feature.

**This is an unofficial modified edition, not affiliated with the Watt Toolkit team (Beyond Dimension).** Please do not report issues of this project to official channels.

</div>

## Scope

- Network acceleration only, restricted to a whitelist of three project groups: **Public CDN, Google Translate, Github**.
- The only acceleration mode is **hosts mode**: system hosts entries point target domains to a local Yarp reverse-proxy subprocess, fronted by a local root certificate. System proxy / PAC modes are not included.
- Menu bar resident icon controls acceleration start/stop and quit; "accelerate on launch" is supported; closing the main window hides it to the background while acceleration keeps running.

## Relation to the upstream app

- Reuses the official identity: bundle id `net.steampp.app`, data directory `~/Library/Steam++`, therefore **it cannot coexist with the official app**.
- Installing this edition after uninstalling the official app inherits existing data (acceleration selections, trusted root certificate, project group cache, launch settings) with zero configuration.

## Build

Requirements: macOS (Apple Silicon), Xcode, .NET SDK with the `net10.0-macos` workload.

```bash
git clone --recursive https://github.com/yuxint/steam-plusplus.git
cd steam-plusplus
./build/macos/build.sh
```

- Output: `src/BD.WTTS.Client.Avalonia.App/bin/Release/net10.0-macos/osx-arm64/Steam++.app`.
- Full build log goes to `/tmp/steampp-macos-build.log` by default (override with `LOG_FILE`).
- Optional code signing: `CODESIGN_IDENTITY="Apple Development: ..." ./build/macos/build.sh` (unsigned by default).

## License

- Licensed under [GNU GPL-3.0](./LICENSE), same as upstream.
- Based on [Watt Toolkit](https://github.com/BeyondDimension/SteamTools) (GPL-3.0); modifications are published under the same license.
- The names Watt Toolkit and Steam++ and related branding belong to their original authors; they are referenced here only to indicate the origin of this derivative work.
