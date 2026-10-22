#!/bin/zsh
set -e
source "${0:A:h}/Enter-DevEnvironment.zsh"
exec python3 "$CATAN_PROJECT_ROOT/tools/development/build_macos.py" "$@"
