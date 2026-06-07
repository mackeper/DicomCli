#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=release-common.sh
source "$SCRIPT_DIR/release-common.sh"

main() {
  [[ $# -eq 0 ]] || die "Usage: scripts/pre-release.sh"

  local base_version
  base_version="$(read_release_version)"

  local version
  version="$(next_rc_version "$base_version")"
  local tag="v$version"

  require_clean_worktree
  require_metadata_version "$base_version"
  require_tag_absent "$tag"
  run_release_checks
  create_release_tag "$tag" "DicomCli $version pre-release"
}

main "$@"
