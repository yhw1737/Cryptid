#!/bin/zsh
# ─────────────────────────────────────────────────────────────
# upload.sh  —  Upload LogicHunter build to Steamworks
#
# Usage:
#   1. Copy your Unity Windows build output into:
#      _SteamDeploy/content/Windows/
#
#   2. Set STEAM_USER to your Steamworks developer account:
#      export STEAM_USER=your_steam_account
#
#   3. Run:
#      chmod +x upload.sh && ./upload.sh
#
# Requires:
#   1. Steamworks SDK at: ~/steamworks_sdk/tools/ContentBuilder/
#      Download: https://partner.steamgames.com/downloads/list
#   2. Rename ./scripts/app_build.vdf.template     → app_build.vdf
#      Rename ./scripts/depot_build_windows.vdf.template → depot_build_windows.vdf
#      Fill in YOUR_APP_ID / YOUR_DEPOT_ID in both files.
# ─────────────────────────────────────────────────────────────

STEAM_USER="${STEAM_USER:-}"
STEAMCMD="$HOME/steamworks_sdk/tools/ContentBuilder/builder_osx/steamcmd.sh"
SCRIPT_DIR="$(cd "$(dirname "$0")/scripts" && pwd)"

if [[ -z "$STEAM_USER" ]]; then
    echo "Error: STEAM_USER 환경 변수를 설정해주세요."
    echo "  export STEAM_USER=your_steam_account"
    exit 1
fi

if [[ ! -f "$STEAMCMD" ]]; then
    echo "Error: steamcmd를 찾을 수 없습니다: $STEAMCMD"
    echo "Steamworks SDK를 다운로드하고 이 스크립트의 STEAMCMD 경로를 수정하세요."
    exit 1
fi

echo "Steam 업로드 시작 (계정: $STEAM_USER) ..."
"$STEAMCMD" \
    +login "$STEAM_USER" \
    +run_app_build "$SCRIPT_DIR/app_build.vdf" \
    +quit
