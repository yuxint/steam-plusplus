#!/usr/bin/env bash
# WattToolkit (Steam++) macos-lite 打包脚本 (Apple Silicon / arm64)
#
# 用法: build/macos/build.sh
#
# 产物: src/BD.WTTS.Client.Avalonia.App/bin/Release/net10.0-macos/osx-arm64/Steam++.app
#       modules 三件套位于 .app/Contents/MonoBundle/modules/Accelerator/
#
# 全量编译日志: /tmp/steampp-macos-build.log (可用 LOG_FILE 覆盖)
# 可选代码签名: CODESIGN_IDENTITY="Apple Development: ..." ./build.sh (默认不签名)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APP_PROJ="$REPO_ROOT/src/BD.WTTS.Client.Avalonia.App"
PLUGIN_PROJ="$REPO_ROOT/src/BD.WTTS.Client.Plugins.Accelerator"
PROXY_PROJ="$REPO_ROOT/src/BD.WTTS.Client.Plugins.Accelerator.ReverseProxy"

APP_BIN="$APP_PROJ/bin/Release/net10.0-macos"
PLUGIN_BIN="$PLUGIN_PROJ/bin/Release/net10.0-macos"
PROXY_PUBLISH="$PROXY_PROJ/bin/Release/net10.0/osx-arm64/publish"

# 本地编译配方：macos workload 26.5 要求 Xcode 26.6 (本机 26.3)，
# 且 Release 裁剪默认 managed-static 注册器需 macOS 26.5 SDK 头文件。
# 升级 Xcode 26.6 后可清空这两个开关换回静态注册器。
MACOS_FLAGS=(-p:ValidateXcodeVersion=false -p:Registrar=dynamic)

LOG_FILE="${LOG_FILE:-/tmp/steampp-macos-build.log}"
: > "$LOG_FILE"

log()  { echo "[build] $*"; }
fail() { echo "[build] 失败: $* (完整日志: $LOG_FILE)" >&2; exit 1; }

dotnet_cmd() {
    echo "[build] \$ $*" | tee -a "$LOG_FILE"
    "$@" >> "$LOG_FILE" 2>&1 || fail "$* 执行失败"
}

APP_RID=osx-arm64
APP_PATH="$APP_BIN/$APP_RID/Steam++.app"
MODULES_DIR="$APP_PATH/Contents/MonoBundle/modules/Accelerator"

# ---- 1. 主程序 ----
log "publish 主程序 ($APP_RID) ..."
dotnet_cmd dotnet publish "$APP_PROJ" -c Release -f net10.0-macos -r "$APP_RID" \
    -p:CreatePackage=false "${MACOS_FLAGS[@]}"
[ -d "$APP_PATH" ] || fail "未找到 $APP_PATH"

# ---- 2. 插件 (不在主程序编译图内, 必须单编; publish 走 .pkg 分支不落 PublishDir, 用 build 产物) ----
log "build 加速插件 (net10.0-macos) ..."
dotnet_cmd dotnet build "$PLUGIN_PROJ" -c Release -f net10.0-macos
PLUGIN_DLL="$PLUGIN_BIN/BD.WTTS.Client.Plugins.Accelerator.dll"
[ -f "$PLUGIN_DLL" ] || fail "未找到插件产物 $PLUGIN_DLL"

# ---- 3. 加速子进程 (Yarp, 单文件自包含) ----
log "publish 加速子进程 ($APP_RID) ..."
dotnet_cmd dotnet publish "$PROXY_PROJ" -c Release -f net10.0 -r "$APP_RID" \
    -p:PublishSingleFile=true --self-contained
[ -f "$PROXY_PUBLISH/Steam++.Accelerator" ] || fail "未找到子进程产物 $PROXY_PUBLISH/Steam++.Accelerator"
[ -f "$PROXY_PUBLISH/libe_sqlite3.dylib" ] || fail "未找到 $PROXY_PUBLISH/libe_sqlite3.dylib"

# ---- 4. 铺 modules 三件套 (publish 会清空 modules/, 必须在主程序 publish 之后) ----
log "铺设 modules/Accelerator 三件套 ..."
rm -rf "$APP_PATH/Contents/MonoBundle/modules"
mkdir -p "$MODULES_DIR"
cp "$PLUGIN_DLL" "$MODULES_DIR/"
cp "$PROXY_PUBLISH/Steam++.Accelerator" "$MODULES_DIR/"
cp "$PROXY_PUBLISH/libe_sqlite3.dylib" "$MODULES_DIR/"
chmod +x "$MODULES_DIR/Steam++.Accelerator"

# ---- 5. 可选签名 ----
if [ -n "${CODESIGN_IDENTITY:-}" ]; then
    log "代码签名 ($CODESIGN_IDENTITY) ..."
    codesign --force --deep --sign "$CODESIGN_IDENTITY" "$APP_PATH"
else
    log "跳过代码签名 (设 CODESIGN_IDENTITY 以启用)"
fi

# ---- 6. 产物核验 ----
log "---- 产物核验 ----"
du -sh "$APP_PATH"
lipo -archs "$MODULES_DIR/Steam++.Accelerator" 2>/dev/null | sed 's/^/[build] 子进程架构: /'
for f in "$MODULES_DIR"/*; do
    printf '[build] %s  %s\n' "$(md5 -q "$f")" "$(basename "$f")"
done
log "完成: $APP_PATH"
