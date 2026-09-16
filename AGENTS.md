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
