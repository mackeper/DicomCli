#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=release-common.sh
source "$SCRIPT_DIR/release-common.sh"

main() {
  [[ $# -eq 1 ]] || die "Usage: scripts/pre-release.sh <version>, for example scripts/pre-release.sh 0.1.0-rc.1"

  local version="$1"
  [[ "$version" =~ ^(0|[1-9][0-9]*)\.([0-9]|[1-9][0-9]*)\.([0-9]|[1-9][0-9]*)-rc\.([1-9][0-9]*)$ ]] || die "Pre-release version must use X.Y.Z-rc.N format."

  local base_version="${version%%-rc.*}"
  local tag="v$version"

  require_clean_worktree
  require_metadata_version "$base_version"
  require_tag_absent "$tag"
  run_release_checks
  create_release_tag "$tag" "DicomCli $version pre-release"
}

main "$@"
