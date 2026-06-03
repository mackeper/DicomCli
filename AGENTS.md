# DicomCli

.NET 10 CLI. Reads DICOM, prints tags as text or DICOMweb JSON. Writes DICOM from DICOMweb JSON. Uses `fo-dicom` 5.2.6.

## Commands

- `make run` - run CLI with 2 GB heap cap
- `make build` - build solution with 2 GB heap cap
- `make format` - format solution with 2 GB heap cap
- `make publish` - publish linux-arm64 single binary to `bin/cli`
- `dotnet test` - run xUnit v3 tests via MTP

## CLI

- `read <file>` or `<file>` - read DICOM file
- `--format text|json` - output text default or DICOMweb JSON
- `--binary-format summary|hex` - binary output summary default or base64/hex detail
- `write <input-json> <output-dicom>` - write DICOM from DICOMweb JSON

## Structure

- `src/cli/` - CLI project, entry `Program.cs`
- `tests/cli.Tests/` - integration tests
- `DicomCli.slnx` - solution
- `0002.DCM` - sample DICOM
- `global.json` - use Microsoft Testing Platform

<developer-review-loop>
## Developer/Reviewer Loop

Every code change, one logical unit:

1. Implement.
2. Validate: `make build`, then `make format`. Failure -> fix + rerun.
3. Review: call `task` with `Reviewer1` or `Reviewer2`.
4. Act: apply good correctness/style/arch suggestions.
5. Re-validate after edits.
6. Repeat review loop max 3 times, or until no findings.
</developer-review-loop>
