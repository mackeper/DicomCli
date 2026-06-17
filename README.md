# DicomCli

Command-line tool for reading and writing DICOM files.

[Usage](#usage) · [Docs](#docs) · [Install](#install) · [Build](#build-from-source) · [Privacy](#privacy) · [License](#license)

## Usage

```bash
dicomcli image.dcm
dicomcli image.dcm --format json
dicomcli image.dcm --format json --compact
dicomcli input.json --output output.dcm
dicomcli compare left.dcm right.dcm
```

Common options: `--format text|json`, `--compact`, `--binary-format summary|hex|base64`, `-o, --output <file>`, `compare`, `--help`.

Exit codes: `0` success/equal, `1` invalid input; for `compare`, `1` means different and `2` means invalid input/read error.

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
