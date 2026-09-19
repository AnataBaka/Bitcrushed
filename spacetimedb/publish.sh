#!/usr/bin/env bash
# Republish the merged module to the Maincloud database the Unity client uses.
# Breaking schema changes (new inventory / combat_event tables) require wiping
# the previous lobby rows.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
export PATH="${DOTNET_ROOT:-$HOME/.dotnet}:$HOME/.local/bin:$PATH"
exec spacetime publish hophacks-party-vp \
  --module-path "$ROOT" \
  --server maincloud \
  --yes \
  --delete-data=always
