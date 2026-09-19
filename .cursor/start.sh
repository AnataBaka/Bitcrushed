#!/usr/bin/env bash
# Per-boot startup for the Hophacks SpacetimeDB backend.
# Launches the local SpacetimeDB server (idempotent), waits for it to
# become ready, then publishes the module as `hophacks-party` so the
# Unity client can connect at http://127.0.0.1:3000.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="/tmp/spacetimedb-server.log"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

spacetime server set-default local >/dev/null 2>&1 || true

echo "==> Ensuring local SpacetimeDB server is running"
if ! spacetime server ping local >/dev/null 2>&1; then
  nohup spacetime start >"$LOG" 2>&1 &
fi

echo "==> Waiting for the server to become ready on :3000"
ready=false
for _ in $(seq 1 90); do
  if spacetime server ping local >/dev/null 2>&1; then
    ready=true
    break
  fi
  sleep 1
done

if [ "$ready" != "true" ]; then
  echo "ERROR: SpacetimeDB server did not become ready" >&2
  tail -n 40 "$LOG" 2>/dev/null || true
  exit 1
fi

echo "==> Publishing the hophacks-party module"
( cd "$REPO_DIR/spacetimedb" && spacetime publish --server local hophacks-party --yes )

echo "==> start.sh complete: server ready and module published"
