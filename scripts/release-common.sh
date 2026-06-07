#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROPS_FILE="$REPO_ROOT/Directory.Build.props"

log_info() {
  printf '[INFO] %s\n' "$1"
}

log_error() {
  printf '[ERROR] %s\n' "$1" >&2
}

die() {
  log_error "$1"
  exit 1
}

read_property() {
  local name="$1"
  local value

  value="$(grep -E "^[[:space:]]*<$name>[^<]+</$name>[[:space:]]*$" "$PROPS_FILE" | sed -E "s|^[[:space:]]*<$name>([^<]+)</$name>[[:space:]]*$|\1|" | head -n 1 || true)"
  [[ -n "$value" ]] || die "Missing <$name> in $PROPS_FILE."
  printf '%s\n' "$value"
}

read_release_version() {
  local version

  version="$(read_property Version)"
  [[ "$version" =~ ^(0|[1-9][0-9]*)\.([0-9]|[1-9][0-9]*)\.([0-9]|[1-9][0-9]*)$ ]] || die "Directory.Build.props <Version> must use X.Y.Z format."
  printf '%s\n' "$version"
}

next_rc_version() {
  local base_version="$1"
  local version_pattern="^v${base_version//./\\.}-rc\.([1-9][0-9]*)$"
  local max_rc=0
  local rc
  local remote_refs
  local remote_status
  local ref
  local tag

  while IFS= read -r tag; do
    [[ -n "$tag" ]] || continue
    if [[ "$tag" =~ $version_pattern ]]; then
      rc="${BASH_REMATCH[1]}"
      if ((rc > max_rc)); then
        max_rc="$rc"
      fi
    fi
  done < <(git -C "$REPO_ROOT" tag --list "v$base_version-rc.*")

  set +e
  remote_refs="$(git -C "$REPO_ROOT" ls-remote --tags origin "refs/tags/v$base_version-rc.*" 2>/dev/null)"
  remote_status=$?
  set -e

  [[ $remote_status -eq 0 ]] || die "Unable to read remote RC tags from origin."

  while IFS=$'\t' read -r _ ref; do
    [[ -n "${ref:-}" ]] || continue
    if [[ "$ref" == *"^{}" ]]; then
      ref="${ref:0:${#ref}-3}"
    fi

    tag="${ref#refs/tags/}"
    if [[ "$tag" =~ $version_pattern ]]; then
      rc="${BASH_REMATCH[1]}"
      if ((rc > max_rc)); then
        max_rc="$rc"
      fi
    fi
  done <<<"$remote_refs"

  printf '%s-rc.%d\n' "$base_version" "$((max_rc + 1))"
}

require_metadata_version() {
  local expected_version="$1"
  local expected_assembly_version="$expected_version.0"
  local version
  local assembly_version
  local file_version
  local informational_version

  version="$(read_property Version)"
  assembly_version="$(read_property AssemblyVersion)"
  file_version="$(read_property FileVersion)"
  informational_version="$(read_property InformationalVersion)"

  [[ "$version" == "$expected_version" ]] || die "Directory.Build.props <Version> is '$version'; expected '$expected_version'."
  [[ "$assembly_version" == "$expected_assembly_version" ]] || die "Directory.Build.props <AssemblyVersion> is '$assembly_version'; expected '$expected_assembly_version'."
  [[ "$file_version" == "$expected_assembly_version" ]] || die "Directory.Build.props <FileVersion> is '$file_version'; expected '$expected_assembly_version'."
  [[ "$informational_version" == "$expected_version" ]] || die "Directory.Build.props <InformationalVersion> is '$informational_version'; expected '$expected_version'."
}

require_clean_worktree() {
  [[ -z "$(git -C "$REPO_ROOT" status --porcelain)" ]] || die "Working tree must be clean before creating a release tag."
}

require_tag_absent() {
  local tag="$1"
  local remote_status

  if git -C "$REPO_ROOT" rev-parse --verify --quiet "refs/tags/$tag" >/dev/null; then
    die "Local tag '$tag' already exists."
  fi

  set +e
  git -C "$REPO_ROOT" ls-remote --exit-code --tags origin "refs/tags/$tag" >/dev/null 2>&1
  remote_status=$?
  set -e

  if [[ $remote_status -eq 0 ]]; then
    die "Remote tag 'origin/$tag' already exists."
  fi

  [[ $remote_status -eq 2 ]] || die "Unable to verify remote tag absence on origin."
}

run_release_checks() {
  log_info "Running release checks..."
  DOTNET_GCHeapHardLimit=7C0000000 dotnet restore "$REPO_ROOT/DicomCli.slnx"
  DOTNET_GCHeapHardLimit=7C0000000 dotnet format "$REPO_ROOT/DicomCli.slnx" --verify-no-changes --no-restore
  DOTNET_GCHeapHardLimit=7C0000000 dotnet build "$REPO_ROOT/DicomCli.slnx" --configuration Release --no-restore
  DOTNET_GCHeapHardLimit=7C0000000 dotnet test --solution "$REPO_ROOT/DicomCli.slnx" --configuration Release --no-build
}

create_release_tag() {
  local tag="$1"
  local message="$2"

  git -C "$REPO_ROOT" tag -a "$tag" -m "$message"
  log_info "Created tag $tag."
  log_info "Push with: git push origin $tag"
}
