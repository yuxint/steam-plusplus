# AGENTS.md — steam-plusplus（WattToolkit macOS 改造版）

## Agent skills

### Issue tracker

本地 markdown：issue 与 spec 以文件形式存于 `.scratch/<feature>/`，不使用远程 issue。见 `docs/agents/issue-tracker.md`。

### Triage labels

默认五标签：needs-triage、needs-info、ready-for-agent、ready-for-human、wontfix。见 `docs/agents/triage-labels.md`。

### Domain docs

Single-context 布局：根目录 `CONTEXT.md`（术语表，已存在）+ `docs/adr/`（决策记录，已存在）。见 `docs/agents/domain.md`。

## 构建与产物清理

每次通过 `build/macos/build.sh` 构建 `.app` 并确认产物就位后，必须清理全仓库的编译中间产物（`src` 下所有工程的 `bin`/`obj` 目录，约 1.5GB）。这些产物不进 git，删除不影响已安装到 `/Applications` 的 `.app`，下次构建从源码本地重新编译（依赖走 `~/.nuget/packages` 本地缓存，不联网下载）。清理命令：

```bash
find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
```

不要清理 `~/.nuget/packages`（NuGet 全局包缓存，删了会触发重新联网下载）。

**安装方式（2026-09-17 起改为实体拷贝，不再用软链）：** `/Applications/Steam++.app` 是实体目录（约 320MB）。软链方式有已验证的事故：删 bin/obj 会让软链悬空，运行中的进程文件被删后，打开主面板触发懒加载资源（`avares://` 流、图标）失败，页面白屏。因清理规则要求删 bin，两者冲突，故改为构建后手动拷贝安装：

```bash
rm -rf /Applications/Steam++.app && cp -R <build输出>/Steam++.app /Applications/Steam++.app
```

注意：build.sh 检测到 `/Applications/Steam++.app` 是实体目录时会跳过自动安装（提示"跳过安装"），属预期行为；更新安装必须手动执行上面的拷贝命令。清理 bin/obj 前确认应用已退出或已从 `/Applications` 启动，绝不能删除正在运行的进程的文件。
