# DicomCli

.NET 10 CLI for reading DICOM files and writing DICOM files from DICOMweb JSON.

Uses `fo-dicom` 5.2.6 and `System.CommandLine`.

Current release status: `0.1.0` initial product metadata and CLI baseline.

Repository URL: <https://github.com/mackeper/DicomCli>.

## Usage

- `dotnet run --project src/cli -- <file>` reads a DICOM file (`.dcm` or `.dicom`).
- `dotnet run --project src/cli -- --version` prints the CLI version.
- `--format text|json` selects text output or DICOMweb JSON output.
- `--binary-format summary|hex|base64` controls binary value output.
- `dotnet run --project src/cli -- <input-json> -o <output-dicom>` writes DICOM from DICOMweb JSON (`.json` to `.dcm` or `.dicom`).

## Privacy

DICOM text and JSON output can contain patient names, identifiers, and other PHI or patient-identifying data. Treat CLI output, logs, copied snippets, and generated files as sensitive clinical data unless you have verified they are de-identified.

## License Metadata

Package and build metadata declare `MIT` with `PackageLicenseExpression`. This step does not generate or include a `LICENSE` file, and the metadata is not legal advice or a substitute for manually reviewed license text.

## Commands

- `make run` runs the CLI with a 2 GB heap cap.
- `make build` builds the solution.
- `make format` formats the solution.
- `make publish` publishes a linux-arm64 single-file binary to `bin/dicomcli`.
- `dotnet test` runs the test project.

## Tests

Tests use xUnit v3 with Microsoft Testing Platform, selected in `global.json`.
