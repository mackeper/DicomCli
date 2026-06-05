# Release Readiness Plan

Current repo has moved from initial CLI prototype toward release candidate shape. Recent commits completed product identity/version output, centralized build metadata, CI quality gates, a tagged release workflow, release artifacts/checksums, initial release docs, command-flow cleanup, and private tag VR preservation. Remaining release gaps are mainly user docs, release verification, and deeper test coverage.

## Completed Recently

1. Product identity
   - Added `Directory.Build.props` with `Product=DicomCli`, `Version=0.1.0`, assembly/file/informational versions, repository metadata, analyzer settings, warnings-as-errors, and deterministic build settings.
   - Added CLI `--version` output using assembly product and informational version metadata.
   - Renamed published binary from generic `cli` to `dicomcli`.
   - Updated `Makefile` publish target to pass `Version` and `InformationalVersion`.

2. Release automation
   - Added `.github/workflows/release.yml` on `v*` tags.
   - Publishes `linux-x64`, `linux-arm64`, and `win-x64` self-contained single-file artifacts.
   - Packages Linux artifacts as `.tar.gz` and Windows artifacts as `.zip`.
   - Generates checksums for release packages.
   - Creates GitHub releases with `gh release create`.
   - Derives release version from SemVer git tags and passes it into `dotnet publish`.
   - Runs published binary smoke tests for native `linux-x64` and `win-x64` artifacts with `--version` and sample DICOM read.

3. Build quality gates
   - Added Release restore/build/test/format verification through `make check`.
   - CI now runs checks on Ubuntu and Windows.
   - CI publish job builds runtime artifacts for `linux-x64`, `linux-arm64`, and `win-x64`.
   - Enabled `TreatWarningsAsErrors`, latest .NET analyzers, code-style enforcement in build, deterministic build, and CI build metadata.

4. CLI and DICOM behavior
   - Simplified command model to implicit read/write flows instead of `read`/`write` subcommands.
   - Required exactly one input file argument.
   - Defaulted text output to binary summaries and DICOMweb JSON output to base64 `InlineBinary`.
   - Preserved unknown/private tag VRs during DICOMweb JSON write/read round trips when private creator context exists.

5. Initial docs and tests
    - Added `CHANGELOG.md` with `0.1.0` and Unreleased entries.
    - Updated `README.md` with usage, version output, privacy warning, and repository URL.
    - Split tests into command-line, read-flow, and write-flow coverage.
    - Added coverage for version output, uppercase extensions, invalid options/extensions, binary format defaults, malformed/unsupported DICOMweb JSON cases, and private tag round trips.
    - Added golden DICOMweb JSON output coverage and a modest 64 KiB binary summary/base64 handling test.

## Highest Remaining Priority

1. Test hardening
    - Done: golden JSON tests and one modest large-binary handling test for summary/base64 behavior without heavy fixtures.

2. User docs
   - Done: Expanded README installation, release artifact, checksum verification, build-from-source, exact help output, exit code, usage example, `BulkDataURI`, troubleshooting, and PHI/privacy docs.
   - Expand README with an Install section for GitHub Releases, a Build From Source section, release artifact names (`dicomcli-linux-x64.tar.gz`, `dicomcli-linux-arm64.tar.gz`, `dicomcli-win-x64.zip`), per-artifact `.sha256` checksum verification using Linux `sha256sum -c` and manual Windows PowerShell `Get-FileHash` comparison, exact current help output, `0`/`1` exit codes, and examples for version, text read, JSON read, and JSON write.
   - Document that release archives include `LICENSE` and `README.md` at the archive root.
   - Use raw `dotnet` commands in README build instructions; do not mention `make` in README because the Makefile is a maintainer/Termux heap-limit helper.
   - Use released binary commands in README usage examples; keep `dotnet run` only in Build From Source.
   - Use generic sample paths such as `image.dcm`.
   - Capture README help output from `dotnet run --project src/cli -- --help` after tests pass.
   - Add short troubleshooting entries for missing .NET 10 SDK and unsupported file extensions.
   - Add a README Limitations section: document `BulkDataURI` as unsupported when writing DICOM from DICOMweb JSON, and reference `fo-dicom` for underlying DICOM parsing behavior.
   - Keep PHI risk in a dedicated README Privacy section covering text/JSON output and generated DICOM files.
   - Keep operational notes limited to PHI risk and artifact verification.

3. Release verification
   - Keep CI publish artifacts as build artifacts rather than final release packages.
   - Include `LICENSE` and `README.md` in both CI publish artifacts and final release archives.
   - Include `LICENSE` and `README.md` at the root of each release archive next to the executable.
   - Add one sidecar checksum file per uploaded CI artifact payload so routine builds can verify downloaded artifact integrity.
   - Name CI checksum sidecars `<payload-name>.sha256`, for example `dicomcli-linux-x64.tar.gz.sha256`.
   - Write `.sha256` files in standard `hash  filename` format.
   - Use per-artifact `.sha256` checksum sidecars for final GitHub release assets too.
   - Keep CI Linux artifacts as `.tar.gz` so checksums cover the same archive format used by releases.
   - Zip the CI Windows publish output so its checksum covers one uploaded payload.
   - Keep Linux artifacts as `.tar.gz` and Windows artifacts as `.zip`, per `DECISIONS.md`.
   - Smoke-test the CI `linux-x64` published binary with `--version` and a checked-in tiny DICOM fixture read.
   - Mark `*-rc.*` tag releases as GitHub pre-releases automatically.
   - Use `tests/fixtures/sample.dcm` for release workflow smoke tests.

## Concrete Files To Update

- `.github/workflows/ci.yml`: include `LICENSE` and `README.md`, add sidecar checksums for publish artifacts, and add a `linux-x64` publish smoke test with a checked-in tiny DICOM fixture.
- `.github/workflows/release.yml`: include `LICENSE` and `README.md` in archives, use per-artifact `.sha256` checksums, mark `*-rc.*` tag releases as GitHub pre-releases, and smoke-test with a checked-in tiny DICOM fixture.
- `tests/fixtures/sample.dcm`: add a tiny DICOM fixture with known redistribution rights for workflow smoke tests.
- `README.md`: expand enterprise-grade usage, release, and limitation docs.
- `CHANGELOG.md`: align `0.1.0` with first-release features only; omit refactoring and bug-fix detail.

## Suggested Execution Order

1. Done: Expand README installation, release, checksum verification, help output, examples, `BulkDataURI`, and PHI docs.
2. Done: Include `LICENSE` and `README.md` in release archives.
3. Done: Add checksums for CI publish artifacts.
4. Add `tests/fixtures/sample.dcm` and use it for CI and release workflow smoke tests.
5. Run `make check` locally and verify release workflow on `v0.1.0-rc.1`; keep the RC release only if it is marked pre-release, otherwise delete it before final `v0.1.0`.
6. Tag first release as `v0.1.0`.
