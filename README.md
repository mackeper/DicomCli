# DicomCli

CLI tools for reading and writing DICOM files.

[Usage](#usage) - [Install](#install) - [Build](#build-from-source) - [Privacy](#privacy) - [License](#license) - [ROADMAP.md](ROADMAP.md)

## Usage

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

Every release includes a `SHA256SUMS` file. On Linux, download it with the release archive, then run:

```bash
sha256sum -c SHA256SUMS
```

On Windows, compare the expected hash in `SHA256SUMS` with PowerShell output:

```powershell
Get-Content .\SHA256SUMS
Get-FileHash .\dicomcli-win-x64.zip -Algorithm SHA256
```

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

Publish a Linux ARM64 binary to `bin/dicomcli` with:

```bash
make publish
```

## Privacy

DICOM files commonly contain protected health information (PHI), patient names, identifiers, birth dates, accession numbers, study details, and institution details.

Treat all DicomCli text output, DICOMweb JSON output, terminal scrollback, redirected output files, logs, screenshots, copied snippets, generated DICOM files, and release-verification sample output as sensitive clinical data unless you have verified the data is de-identified.

DicomCli does not de-identify data. Do not paste command output or generated JSON/DICOM files into tickets, chats, logs, or public issue trackers unless PHI risk has been reviewed.

## License

DicomCli is licensed under the MIT License. See `LICENSE`.
