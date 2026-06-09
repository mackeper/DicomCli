#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if [[ -n "${TERMUX_VERSION:-}" || "${PREFIX:-}" == "/data/data/com.termux/files/usr" || (-d "/data/data/com.termux/files/usr" && -n "${ANDROID_ROOT:-}" && -n "${ANDROID_DATA:-}") ]]; then
  export DOTNET_GCHeapHardLimit=7C0000000
fi
