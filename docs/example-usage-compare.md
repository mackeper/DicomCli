# Example Usage: Compare DICOM Files

DicomCli can compare two `.dcm` or `.dicom` files by flattened DICOM attribute path. It prints only differences.

Examples use `left.dcm` and `right.dcm`. Replace them with your own files.

## Quick Compare

```bash
dicomcli left.dcm --compare right.dcm
```

Example output:

```text
~ 00100010 PN Patient's Name
  left:  Doe^Jane
  right: Doe^John
+ 00100020 LO Patient ID
  right: 12345
- 0020000D UI Study Instance UID
  left:  1.2.3
```

No output means no differences were found. Compare uses diff-style exit codes: `0` equal, `1` different, `2` invalid input or unreadable file.

## External Text Compare

Linux/macOS:

```bash
dicomcli left.dcm > left.txt
dicomcli right.dcm > right.txt
diff -u left.txt right.txt
```

PowerShell:

```powershell
dicomcli left.dcm > left.txt
dicomcli right.dcm > right.txt
Compare-Object (Get-Content left.txt) (Get-Content right.txt)
```

Text output is best for a quick human-readable header comparison. Binary attributes are summarized by default, which keeps pixel data from flooding the diff.

## Compare Common Header Tags

Filter both files to the tags you care about before comparing.

Linux/macOS:

```bash
dicomcli left.dcm | grep -Ei "Patient|Study|Series|Modality" > left-summary.txt
dicomcli right.dcm | grep -Ei "Patient|Study|Series|Modality" > right-summary.txt
diff -u left-summary.txt right-summary.txt
```

PowerShell:

```powershell
dicomcli left.dcm | Select-String "Patient|Study|Series|Modality" | ForEach-Object { $_.Line } | Set-Content left-summary.txt -Encoding utf8
dicomcli right.dcm | Select-String "Patient|Study|Series|Modality" | ForEach-Object { $_.Line } | Set-Content right-summary.txt -Encoding utf8
Compare-Object (Get-Content left-summary.txt) (Get-Content right-summary.txt)
```

## Compare Exact Tags

Use rendered text tags when you need specific attributes.

Linux/macOS:

```bash
dicomcli left.dcm | grep -Ei "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" > left-tags.txt
dicomcli right.dcm | grep -Ei "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" > right-tags.txt
diff -u left-tags.txt right-tags.txt
```

PowerShell:

```powershell
dicomcli left.dcm | Select-String "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" | ForEach-Object { $_.Line } | Set-Content left-tags.txt -Encoding utf8
dicomcli right.dcm | Select-String "^\((0010,0010|0010,0020|0020,000d|0020,000e)\)" | ForEach-Object { $_.Line } | Set-Content right-tags.txt -Encoding utf8
Compare-Object (Get-Content left-tags.txt) (Get-Content right-tags.txt)
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
dicomcli left.dcm --format json > left.json
dicomcli right.dcm --format json > right.json
diff -u left.json right.json
```

PowerShell:

```powershell
dicomcli left.dcm --format json > left.json
dicomcli right.dcm --format json > right.json
Compare-Object (Get-Content left.json) (Get-Content right.json)
```

Use pretty JSON for review. `--compact` is better when another command needs one JSON document per line.

## Notes

Raw binary file comparison can report differences that are not useful for tag review, such as encoding or metadata changes. Compare rendered text or DICOMweb JSON when you care about DICOM attributes.

Generated text and JSON can contain protected health information (PHI). Treat comparison outputs as sensitive unless both source files are verified de-identified.
