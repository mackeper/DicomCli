# Example Usage: Compare DICOM Files

DicomCli prints DICOM attributes as text or DICOMweb JSON. Use those outputs with standard diff tools to compare two `.dcm` or `.dicom` files.

Examples use `before.dcm` and `after.dcm`. Replace them with your own files.

## Quick Text Compare

Linux/macOS:

```bash
dicomcli before.dcm > before.txt
dicomcli after.dcm > after.txt
diff -u before.txt after.txt
```

PowerShell:

```powershell
dicomcli before.dcm > before.txt
dicomcli after.dcm > after.txt
Compare-Object (Get-Content before.txt) (Get-Content after.txt)
```

Text output is best for a quick human-readable header comparison. Binary attributes are summarized by default, which keeps pixel data from flooding the diff.

## Compare Common Header Tags

Filter both files to the tags you care about before comparing.

Linux/macOS:

```bash
dicomcli before.dcm | grep -Ei "Patient|Study|Series|Modality" > before-summary.txt
dicomcli after.dcm | grep -Ei "Patient|Study|Series|Modality" > after-summary.txt
diff -u before-summary.txt after-summary.txt
```

PowerShell:

```powershell
dicomcli before.dcm | Select-String "Patient|Study|Series|Modality" | ForEach-Object { $_.Line } | Set-Content before-summary.txt -Encoding utf8
dicomcli after.dcm | Select-String "Patient|Study|Series|Modality" | ForEach-Object { $_.Line } | Set-Content after-summary.txt -Encoding utf8
Compare-Object (Get-Content before-summary.txt) (Get-Content after-summary.txt)
```

## Compare Exact Tags

Use rendered text tags when you need specific attributes.

Linux/macOS:

```bash
dicomcli before.dcm | grep -Ei "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" > before-tags.txt
dicomcli after.dcm | grep -Ei "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" > after-tags.txt
diff -u before-tags.txt after-tags.txt
```

PowerShell:

```powershell
dicomcli before.dcm | Select-String "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" | ForEach-Object { $_.Line } | Set-Content before-tags.txt -Encoding utf8
dicomcli after.dcm | Select-String "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" | ForEach-Object { $_.Line } | Set-Content after-tags.txt -Encoding utf8
Compare-Object (Get-Content before-tags.txt) (Get-Content after-tags.txt)
```

Common tags:

| Tag | Name |
|-----|------|
| `(0010,0010)` | Patient's Name |
| `(0010,0020)` | Patient ID |
| `(0008,0060)` | Modality |
| `(0020,000d)` | Study Instance UID |
| `(0020,000e)` | Series Instance UID |

## Compare DICOMweb JSON

JSON output is useful when another tool expects DICOMweb JSON or when you want to compare structured output.

Linux/macOS:

```bash
dicomcli before.dcm --format json > before.json
dicomcli after.dcm --format json > after.json
diff -u before.json after.json
```

PowerShell:

```powershell
dicomcli before.dcm --format json > before.json
dicomcli after.dcm --format json > after.json
Compare-Object (Get-Content before.json) (Get-Content after.json)
```

Use pretty JSON for review. `--compact` is better when another command needs one JSON document per line.

## Notes

Raw binary file comparison can report differences that are not useful for tag review, such as encoding or metadata changes. Compare rendered text or DICOMweb JSON when you care about DICOM attributes.

Generated text and JSON can contain protected health information (PHI). Treat comparison outputs as sensitive unless both source files are verified de-identified.
