#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=release-common.sh
source "$SCRIPT_DIR/release-common.sh"

main() {
  [[ $# -eq 0 ]] || die "Usage: scripts/release.sh"

  local version
  version="$(read_release_version)"

  local tag="v$version"

  require_clean_worktree
  require_metadata_version "$version"
  require_tag_absent "$tag"
  run_release_checks
  create_release_tag "$tag" "DicomCli $version release"
}

main "$@"
