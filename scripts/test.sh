#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=dotnet-env.sh
source "$SCRIPT_DIR/dotnet-env.sh"

dotnet test --solution "$REPO_ROOT/DicomCli.slnx" --configuration Release
