#!/bin/zsh
# macOS 专用环境；从仓库位置推导工具目录，也允许环境变量覆盖。
export CATAN_PROJECT_ROOT="${${(%):-%x}:A:h:h:h}"
export CATAN_DEVELOPMENT_ROOT="${CATAN_DEVELOPMENT_ROOT:-${CATAN_PROJECT_ROOT:h:h}/Development}"
export CATAN_LOCAL_ROOT="$CATAN_PROJECT_ROOT/.local/development"
export DOTNET_ROOT="$CATAN_DEVELOPMENT_ROOT/dotnet"
export DOTNET_CLI_HOME="$CATAN_LOCAL_ROOT/caches/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1
export NUGET_PACKAGES="$CATAN_LOCAL_ROOT/caches/NuGet/packages"
export NUGET_HTTP_CACHE_PATH="$CATAN_LOCAL_ROOT/caches/NuGet/http"
export NUGET_PLUGINS_CACHE_PATH="$CATAN_LOCAL_ROOT/caches/NuGet/plugins"
export UPM_CACHE_ROOT="$CATAN_LOCAL_ROOT/caches/Unity/upm"
export ASSETSTORE_CACHE_PATH="$CATAN_LOCAL_ROOT/caches/Unity/AssetStore"
export BLENDER_USER_CONFIG="$CATAN_LOCAL_ROOT/blender/config"
export BLENDER_USER_SCRIPTS="$CATAN_LOCAL_ROOT/blender/scripts"
export BLENDER_USER_DATAFILES="$CATAN_LOCAL_ROOT/blender/datafiles"
export UNITY_EDITOR="${UNITY_EDITOR:-$CATAN_DEVELOPMENT_ROOT/Unity/6000.3.25f1/Unity.app/Contents/MacOS/Unity}"
export UNITY_HUB="$CATAN_DEVELOPMENT_ROOT/Unity/Unity Hub.app/Contents/MacOS/Unity Hub"
export CATAN_ANDROID_PLAYER="${UNITY_EDITOR:h:h}/PlaybackEngines/AndroidPlayer"
export ANDROID_HOME="$CATAN_ANDROID_PLAYER/SDK" ANDROID_SDK_ROOT="$CATAN_ANDROID_PLAYER/SDK"
export ANDROID_NDK_ROOT="$CATAN_ANDROID_PLAYER/NDK" JAVA_HOME="$CATAN_ANDROID_PLAYER/OpenJDK"
export GRADLE_USER_HOME="$CATAN_LOCAL_ROOT/caches/gradle" ANDROID_USER_HOME="$CATAN_LOCAL_ROOT/android"
# 使用仓库哈希隔离 socket 短路径；实际临时文件仍保存在仓库内。
local catan_tmp_tag=$(print -rn -- "$CATAN_PROJECT_ROOT" | shasum | cut -c1-12)
local catan_tmp_link="/tmp/catan-$UID-$catan_tmp_tag"
mkdir -p "$CATAN_LOCAL_ROOT/temp"
if [[ ! -e "$catan_tmp_link" && ! -L "$catan_tmp_link" ]]; then
    ln -s "$CATAN_LOCAL_ROOT/temp" "$catan_tmp_link"
fi
if [[ ! -L "$catan_tmp_link" || "${catan_tmp_link:A}" != "$CATAN_LOCAL_ROOT/temp" ]]; then
    print -u2 '项目临时目录短链接被占用，请检查 /tmp/catan-*。'
    return 1
fi
export TMPDIR="$catan_tmp_link/"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$DOTNET_ROOT:$PATH"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH" "$NUGET_PLUGINS_CACHE_PATH" \
    "$UPM_CACHE_ROOT" "$ASSETSTORE_CACHE_PATH" "$BLENDER_USER_CONFIG" "$BLENDER_USER_SCRIPTS" \
    "$BLENDER_USER_DATAFILES" "$GRADLE_USER_HOME" "$ANDROID_USER_HOME" "$CATAN_LOCAL_ROOT/logs" \
    "$CATAN_LOCAL_ROOT/unity-hub" "$CATAN_LOCAL_ROOT/vscode"
