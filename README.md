# DicomCli

.NET 10 CLI for reading DICOM files and writing DICOM files from DICOMweb JSON.

DicomCli uses `fo-dicom` 5.2.6 for DICOM parsing and writing, and `System.CommandLine` for CLI parsing.

Current release status: `0.1.0` initial release candidate.

Repository URL: <https://github.com/mackeper/DicomCli>.

## Install

Download the archive for your platform from GitHub Releases:

- `dicomcli-linux-x64.tar.gz`
- `dicomcli-linux-arm64.tar.gz`
- `dicomcli-win-x64.zip`

Each release archive includes the `dicomcli` executable, `LICENSE`, and `README.md` at the archive root. Windows archives include `dicomcli.exe`.

### Linux

```bash
tar -xzf dicomcli-linux-x64.tar.gz
./dicomcli --version
```

For ARM64 Linux, use `dicomcli-linux-arm64.tar.gz` instead.

### Windows

Extract `dicomcli-win-x64.zip`, then run:

```powershell
.\dicomcli.exe --version
```

## Verify Checksums

Every release includes a `SHA256SUMS` file with checksums for all release archives.

### Linux

Download the archive and `SHA256SUMS` into the same directory, then run:

```bash
sha256sum -c SHA256SUMS
```

Expected output ends with:

```text
dicomcli-linux-x64.tar.gz: OK
```

### Windows

Download `dicomcli-win-x64.zip` and `SHA256SUMS`, then compare the expected hash in `SHA256SUMS` with PowerShell output:

```powershell
Get-Content .\SHA256SUMS
Get-FileHash .\dicomcli-win-x64.zip -Algorithm SHA256
```

The hash values must match exactly, ignoring case.

## Build From Source

Install the .NET 10 SDK, clone the repository, then run:

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/cli -- --version
```

Run from source with:

```bash
dotnet run --project src/cli -- image.dcm
```

Publish a self-contained binary with raw `dotnet publish` commands. Example for Linux x64:

```bash
dotnet publish src/cli/cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:AssemblyName=dicomcli -p:Version=0.1.0 -p:InformationalVersion=0.1.0
```

## Usage

Read a DICOM file as text:

```bash
dicomcli image.dcm
```

Read a DICOM file as DICOMweb JSON:

```bash
dicomcli image.dcm --format json
```

Write a DICOM file from DICOMweb JSON:

```bash
dicomcli image.json -o image.dcm
```

Print version information:

```bash
dicomcli --version
```

Current version output:

```text
DicomCli 0.1.0
```

### Options

- `--format text|json` selects text output or DICOMweb JSON output. Default is `text`.
- `--binary-format summary|hex|base64` controls binary value output when reading DICOM files.
- `-o|--output <output-dicom>` writes DICOM from DICOMweb JSON input.

For text output, binary values default to `summary`. For DICOMweb JSON output, binary values default to base64 `InlineBinary`.

### Help Output

Captured from `dotnet run --project src/cli -- --help`:

```text
Description:
  Reads DICOM files and writes DICOM files from DICOMweb JSON

Usage:
  DicomCli <file> [options]

Arguments:
  <file>  Path to input DICOM or DICOMweb JSON file

Options:
  -f, --format <json|text>              Output format: text or json [default: text]
  --binary-format <base64|hex|summary>  Binary value format: summary, hex, or base64
  -o, --output <output>                 Path to output DICOM file. When present, input file must be DICOMweb JSON.
  --version                             Show version information
  -?, -h, --help                        Show help and usage information
```

### Exit Codes

- `0` means the command completed successfully.
- `1` means the command failed because input, options, file extension, DICOM parsing, or DICOMweb JSON conversion was invalid.

## Limitations

- DICOM read input must use `.dcm` or `.dicom`.
- DICOMweb JSON write input must use `.json`, and output must use `.dcm` or `.dicom`.
- Writing DICOM from DICOMweb JSON does not support `BulkDataURI`. Use `InlineBinary` for binary values.
- Underlying DICOM parsing and writing behavior comes from `fo-dicom` 5.2.6.

## Privacy

DICOM files commonly contain protected health information (PHI), patient names, identifiers, birth dates, accession numbers, study details, and institution details.

Treat all DicomCli text output, DICOMweb JSON output, terminal scrollback, redirected output files, logs, screenshots, copied snippets, generated DICOM files, and release-verification sample output as sensitive clinical data unless you have verified the data is de-identified.

DicomCli does not de-identify data. Do not paste command output or generated JSON/DICOM files into tickets, chats, logs, or public issue trackers unless PHI risk has been reviewed.

## Troubleshooting

### Missing .NET 10 SDK

If source builds fail because `dotnet` cannot find the required SDK, install the .NET 10 SDK and verify it with:

```bash
dotnet --list-sdks
```

Release archives are self-contained and do not require the .NET SDK.

### Unsupported File Extension

DicomCli validates file extensions before reading or writing. Use `.dcm` or `.dicom` for DICOM files and `.json` for DICOMweb JSON input.

Examples:

```bash
dicomcli image.dcm
dicomcli image.json -o image.dcm
```

## License

DicomCli is licensed under the MIT License. See `LICENSE`.
