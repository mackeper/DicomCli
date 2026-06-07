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

  value="$(grep -E "^[[:space:]]*<$name>[^<]+</$name>[[:space:]]*$" "$PROPS_FILE" | sed -E "s|^[[:space:]]*<$name>([^<]+)</$name>[[:space:]]*$|\1|" | head -n 1)"
  [[ -n "$value" ]] || die "Missing <$name> in $PROPS_FILE."
  printf '%s\n' "$value"
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
