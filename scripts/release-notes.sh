#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CHANGELOG_FILE="$REPO_ROOT/CHANGELOG.md"

die() {
  printf '[ERROR] %s\n' "$1" >&2
  exit 1
}

write_changelog_section() {
  local section="$1"

  awk -v section="$section" '
    /^## / {
      heading = $0
      sub(/^##[[:space:]]+/, "", heading)
      split(heading, parts, /[[:space:]]+-[[:space:]]+/)
      heading_key = parts[1]

      if (in_section) {
        exit
      }

      if (heading_key == section) {
        in_section = 1
      }
    }

    in_section { print }
  ' "$CHANGELOG_FILE"
}

main() {
  [[ $# -eq 1 ]] || die "Usage: scripts/release-notes.sh <release-tag>"
  [[ -f "$CHANGELOG_FILE" ]] || die "Missing changelog: $CHANGELOG_FILE"

  local release_tag="$1"
  local release_version="${release_tag#v}"
  local changelog_section="${release_version%%+*}"
  changelog_section="${changelog_section%%-*}"

  local changelog_text
  changelog_text="$(write_changelog_section "$changelog_section")"
  if [[ -z "$changelog_text" ]]; then
    changelog_text="$(write_changelog_section Unreleased)"
  fi
  [[ -n "$changelog_text" ]] || die "No changelog section found for '$changelog_section' or 'Unreleased'."

  printf 'Release %s.\n\n' "$release_tag"
  printf '%s\n' "$changelog_text"
  printf '\nSee the [changelog](https://github.com/OWNER/REPO/blob/TAG/CHANGELOG.md) for full details.\n'
}

main "$@"
