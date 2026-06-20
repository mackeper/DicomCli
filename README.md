# DicomCli

Command-line tool for reading and writing DICOM files.

[Usage](#usage) · [Docs](#docs) · [Install](#install) · [Build](#build-from-source) · [Privacy](#privacy) · [License](#license)

## Usage

```bash
dicomcli image.dcm
dicomcli image.dcm --format json
dicomcli image.dcm --format json --compact
dicomcli image.dcm --extract 7FE00010:base64
dicomcli input.json --output output.dcm
dicomcli left.dcm -c right.dcm
```

Common options: `--format text|json`, `--compact`, `--binary-format summary|hex|base64`, `--extract <tag>:base64|hex|xml`, `-o, --output <file>`, `-c, --compare <file>`, `--help`.

Exit codes:

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Validation failure |
| `2` | Invalid arguments or options |
| `3` | Input file missing or unreadable |
| `4` | Invalid DICOM input |
| `5` | Invalid JSON or DICOMweb JSON |
| `6` | Write failure |
| `7` | Compare found differences |

## Docs

- [Example usage](docs/example-usage.md)

## Install

Download a release archive from [GitHub Releases](https://github.com/mackeper/DicomCli/releases):

| Platform | Archive |
|----------|---------|
| Linux x64 | `dicomcli-linux-x64.tar.gz` |
| Linux ARM64 | `dicomcli-linux-arm64.tar.gz` |
| Windows x64 | `dicomcli-win-x64.zip` |

Each archive contains the binary. Verify downloads against `SHA256SUMS`.

Windows PowerShell install:

```powershell
irm https://raw.githubusercontent.com/mackeper/DicomCli/main/scripts/install.ps1 | iex
```

## Build from Source

Requires .NET 10 SDK.

```bash
# Linux/macOS
scripts/publish.sh
./bin/dicomcli image.dcm
```

```powershell
# Windows
pwsh scripts/publish.ps1
.\bin\dicomcli.exe image.dcm
```

The publish script detects the current OS and CPU architecture. Pass a runtime identifier to override it, such as `scripts/publish.sh linux-x64` or `pwsh scripts/publish.ps1 win-x64`.

## Privacy

DICOM files often contain protected health information (PHI). DicomCli does not de-identify data. Treat output, logs, screenshots, and generated files as sensitive unless verified de-identified.

## License

MIT. See [LICENSE](LICENSE).
