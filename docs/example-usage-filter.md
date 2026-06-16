# Example Usage: Filter Text Output

DicomCli text output prints one DICOM attribute per line, which makes it easy to filter with `grep` on Linux/macOS or `Select-String` in PowerShell.

Examples use `tests/fixtures/sample.dcm`. Replace it with any `.dcm` or `.dicom` file.

## Quick Inspection

Linux/macOS:

```bash
dicomcli image.dcm | grep -Ei "Patient|Study|Series|Modality"
```

PowerShell:

```powershell
dicomcli image.dcm | Select-String "Patient|Study|Series|Modality"
```

## Filter By Tag

Use the rendered DICOM tag when you know the exact attribute.

Linux/macOS:

```bash
dicomcli image.dcm | grep -i "(0010,0010)"
dicomcli image.dcm | grep -i "(0010,0020)"
```

PowerShell:

```powershell
dicomcli image.dcm | Select-String "\(0010,0010\)"
dicomcli image.dcm | Select-String "\(0010,0020\)"
```

Common patient and study tags:

| Tag | Name |
|-----|------|
| `(0010,0010)` | Patient's Name |
| `(0010,0020)` | Patient ID |
| `(0008,0060)` | Modality |
| `(0020,000d)` | Study Instance UID |
| `(0020,000e)` | Series Instance UID |

## Filter By Group

Group related attributes by DICOM group number.

Linux/macOS:

```bash
# Patient group
dicomcli image.dcm | grep -Ei "^\(0010,"

# Study and series group
dicomcli image.dcm | grep -Ei "^\(0020,"
```

PowerShell:

```powershell
# Patient group
dicomcli image.dcm | Select-String "^\(0010,"

# Study and series group
dicomcli image.dcm | Select-String "^\(0020,"
```

## Exclude Binary Data

Text output summarizes binary attributes by default, but you can exclude them when scanning headers.

Linux/macOS:

```bash
dicomcli image.dcm | grep -vi "Pixel Data"
```

PowerShell:

```powershell
dicomcli image.dcm | Select-String "Pixel Data" -NotMatch
```

## Save Filtered Output

Keep a small text extract for review or comparison. Treat output as sensitive unless the source DICOM is verified de-identified.

Linux/macOS:

```bash
dicomcli image.dcm | grep -Ei "Patient|Study|Series|Modality" > dicom-summary.txt
```

PowerShell:

```powershell
dicomcli image.dcm | Select-String "Patient|Study|Series|Modality" | ForEach-Object { $_.Line } | Set-Content dicom-summary.txt
```

## Repository Fixture

From a source checkout, use the run scripts without installing the binary first.

Linux/macOS:

```bash
scripts/run.sh tests/fixtures/sample.dcm | grep -Ei "Patient|Study|Series|Modality"
```

PowerShell:

```powershell
pwsh scripts/run.ps1 tests/fixtures/sample.dcm | Select-String "Patient|Study|Series|Modality"
```

## JSON Note

`--format json` emits pretty DICOMweb JSON by default. Use `--format json --compact` for one-line JSON, or prefer text output for line-oriented `grep` and `Select-String` workflows.
