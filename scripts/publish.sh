#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=dotnet-env.sh
source "$SCRIPT_DIR/dotnet-env.sh"

die() {
  printf '[ERROR] %s\n' "$1" >&2
  exit 1
}

default_runtime() {
  local os
  local arch

  case "$(uname -s)" in
  Linux*) os="linux" ;;
  Darwin*) os="osx" ;;
  *) die "Unsupported OS: $(uname -s)" ;;
  esac

  case "$(uname -m)" in
  x86_64 | amd64) arch="x64" ;;
  aarch64 | arm64) arch="arm64" ;;
  *) die "Unsupported architecture: $(uname -m)" ;;
  esac

  printf '%s-%s\n' "$os" "$arch"
}

runtime="${1:-$(default_runtime)}"

rm -f "$REPO_ROOT/bin/cli" "$REPO_ROOT/bin/cli.exe" "$REPO_ROOT/bin/dicomcli" "$REPO_ROOT/bin/dicomcli.exe"
dotnet publish "$REPO_ROOT/src/cli/cli.csproj" \
  --configuration Release \
  --runtime "$runtime" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:StripSymbols=true \
  -p:InvariantGlobalization=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --output "$REPO_ROOT/bin"

if [[ "$runtime" == win-* ]]; then
  printf 'Run with: %s\n' "$REPO_ROOT/bin/dicomcli.exe"
else
  printf 'Run with: %s\n' "$REPO_ROOT/bin/dicomcli"
fi
