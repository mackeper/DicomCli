# DicomCli

CLI tools for reading and writing DICOM files.

[Usage](#usage) · [Install](#install) · [Build](#build-from-source) · [Privacy](#privacy) · [License](#license)

## Usage

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

Exit codes: `0` success, `1` invalid input.

## Install

Download from [GitHub Releases](https://github.com/anomalyco/DicomCli/releases):

| Platform | Archive |
|----------|---------|
| Linux x64 | `dicomcli-linux-x64.tar.gz` |
| Linux ARM64 | `dicomcli-linux-arm64.tar.gz` |
| Windows x64 | `dicomcli-win-x64.zip` |

Each archive contains the `dicomcli` binary, `LICENSE`, and `README.md`.

```bash
# Linux
tar -xzf dicomcli-linux-x64.tar.gz
./dicomcli --version

# Windows (PowerShell)
# Expand-Archive dicomcli-win-x64.zip
# .\dicomcli.exe --version
```

Verify checksums with `sha256sum -c SHA256SUMS` (Linux) or compare the expected hash from `SHA256SUMS` with `Get-FileHash` (Windows).

## Build from Source

Requires .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/cli -- image.dcm
make publish    # publish linux-arm64 binary to bin/dicomcli
```

## Privacy

DICOM files often contain protected health information (PHI). Treat all DicomCli output—text, JSON, scrollback, files, logs, screenshots—as sensitive clinical data unless verified de-identified. DicomCli does not de-identify data. Do not paste output into tickets, chats, or public trackers without PHI review.

## License

MIT. See [LICENSE](LICENSE).
