#!/usr/bin/env bash
# Idempotent bootstrap for the Hophacks SpacetimeDB backend module.
# Installs the .NET 8 SDK, the wasi-experimental workload, and the
# SpacetimeDB CLI, then restores/builds the module to warm caches.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_DIR="$HOME/.dotnet"
DOTNET_VERSION="8.0.100"

export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "==> Ensuring .NET ${DOTNET_VERSION} SDK is installed"
if [ ! -x "$DOTNET_DIR/dotnet" ] || ! "$DOTNET_DIR/dotnet" --list-sdks | grep -q '^8\.'; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --version "$DOTNET_VERSION" --install-dir "$DOTNET_DIR"
fi

echo "==> Ensuring wasi-experimental workload is installed"
if ! dotnet workload list 2>/dev/null | grep -qi 'wasi-experimental'; then
  dotnet workload install wasi-experimental
fi

echo "==> Ensuring SpacetimeDB CLI is installed"
if [ ! -x "$HOME/.local/bin/spacetime" ]; then
  curl -sSf https://install.spacetimedb.com | sh -s -- -y
fi

echo "==> Selecting the local SpacetimeDB server as default"
"$HOME/.local/bin/spacetime" server set-default local >/dev/null 2>&1 || true

echo "==> Restoring and building the SpacetimeDB module"
( cd "$REPO_DIR/spacetimedb" && "$HOME/.local/bin/spacetime" build )

echo "==> Persisting toolchain PATH for future shells"
for PROFILE in "$HOME/.bashrc" "$HOME/.profile"; do
  MARKER="# >>> hophacks cloud env >>>"
  if ! grep -qF "$MARKER" "$PROFILE" 2>/dev/null; then
    cat >> "$PROFILE" <<EOF

$MARKER
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:\$HOME/.local/bin:\$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
# <<< hophacks cloud env <<<
EOF
  fi
done

echo "==> install.sh complete"
