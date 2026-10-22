#!/bin/zsh
set -e
source "${0:A:h}/Enter-DevEnvironment.zsh"
cd "$CATAN_PROJECT_ROOT"
case "${1:-code}" in
    shell) exec /bin/zsh -i ;;
    code) exec "$CATAN_DEVELOPMENT_ROOT/VSCode/Visual Studio Code.app/Contents/Resources/app/bin/code" --user-data-dir "$CATAN_LOCAL_ROOT/vscode" "$CATAN_PROJECT_ROOT" ;;
    hub) exec "$UNITY_HUB" --user-data-dir="$CATAN_LOCAL_ROOT/unity-hub" ;;
    unity) exec "$UNITY_EDITOR" -projectPath "$CATAN_PROJECT_ROOT/Unity/Catan" -logFile "$CATAN_LOCAL_ROOT/logs/unity-editor.log" ;;
    blender) exec "$CATAN_DEVELOPMENT_ROOT/Blender/Blender.app/Contents/MacOS/Blender" ;;
    build) exec dotnet build src/Catan.Server/Catan.Server.csproj --disable-build-servers -m:1 ;;
    check) exec python3 "$CATAN_PROJECT_ROOT/tools/development/build_macos.py" --check ;;
    *) print -u2 '用法：Start-Dev.command [shell|code|hub|unity|blender|build|check]'; exit 2 ;;
esac
