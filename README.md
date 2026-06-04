# DicomCli

.NET 10 CLI for reading DICOM files and writing DICOM files from DICOMweb JSON.

Uses `fo-dicom` 5.2.6 and `System.CommandLine`.

## Usage

- `dotnet run --project src/cli -- <file>` reads a DICOM file (`.dcm` or `.dicom`).
- `--format text|json` selects text output or DICOMweb JSON output.
- `--binary-format summary|hex|base64` controls binary value output.
- `dotnet run --project src/cli -- <input-json> -o <output-dicom>` writes DICOM from DICOMweb JSON (`.json` to `.dcm` or `.dicom`).

## Commands

- `make run` runs the CLI with a 2 GB heap cap.
- `make build` builds the solution.
- `make format` formats the solution.
- `make publish` publishes a linux-arm64 single-file binary to `bin/cli`.
- `dotnet test` runs the test project.

## Tests

Tests use xUnit v3 with Microsoft Testing Platform, selected in `global.json`.
