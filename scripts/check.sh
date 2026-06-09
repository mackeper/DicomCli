#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=dotnet-env.sh
source "$SCRIPT_DIR/dotnet-env.sh"

dotnet restore "$REPO_ROOT/DicomCli.slnx"
dotnet format "$REPO_ROOT/DicomCli.slnx" --verify-no-changes --no-restore
dotnet build "$REPO_ROOT/DicomCli.slnx" --configuration Release --no-restore
dotnet test --solution "$REPO_ROOT/DicomCli.slnx" --configuration Release --no-build
