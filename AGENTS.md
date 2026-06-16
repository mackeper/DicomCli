# DicomCli

.NET 10 CLI. Read DICOM, print tags as text or DICOMweb JSON. Write DICOM from DICOMweb JSON. Uses `fo-dicom` 5.2.6.

## Commands

- `scripts/run.sh` / `pwsh scripts/run.ps1` - run CLI
- `scripts/build.sh` / `pwsh scripts/build.ps1` - build
- `scripts/format.sh` / `pwsh scripts/format.ps1` - format
- `scripts/test.sh` / `pwsh scripts/test.ps1` - test
- `scripts/check.sh` / `pwsh scripts/check.ps1` - restore, verify format, build, test
- `scripts/publish.sh [runtime]` / `pwsh scripts/publish.ps1 [runtime]` - publish single binary to `bin/`
- `dotnet test` - run xUnit v3 tests via MTP

Bash scripts set `DOTNET_GCHeapHardLimit=7C0000000` only under Termux on Android.

## CLI

- `<file>` - read DICOM (`.dcm` or `.dicom`)
- `--format text|json` - output text default or DICOMweb JSON
- `--binary-format summary|hex|base64` - binary output: summary default, hex/base64 detail
- `<input-json> -o|--output <output-dicom>` - write DICOM from DICOMweb JSON (`.json` to `.dcm` or `.dicom`)

## Structure

- `src/cli/` - CLI project, entry: `Program.cs`
- `tests/cli.Tests/` - integration tests
- `DicomCli.slnx` - solution
- `0002.DCM` - sample DICOM
- `global.json` - use Microsoft Testing Platform

## GitHub Issues

- Before issue create, search open + closed duplicates: `gh issue list --state all --search "<keywords>"`.
- Duplicate or near-duplicate exists: update/comment existing issue. Do not create new one.
- New issue: follow matching template in `.github/ISSUE_TEMPLATE/`. Preserve intent + required sections.

<developer-review-loop>
## Developer/Reviewer Loop

Every code change, one logical unit:

1. Implement.
2. Validate: run `scripts/build.sh`. Run `scripts/format.sh` + `scripts/test.sh` only when C# changed. Failure -> fix + rerun.
3. Review: call `task` with `Reviewer1` or `Reviewer2`.
4. Act: apply good correctness/style/arch suggestions.
5. Re-validate after edits. Same C#-change rule for format/tests.
6. Repeat review loop max 3 times, or until no findings.
</developer-review-loop>
