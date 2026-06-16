# Example Usage: Edit Through JSON

DicomCli can export a DICOM file as DICOMweb JSON, then write edited DICOMweb JSON back to a `.dcm` or `.dicom` file.

Examples use `image.dcm`. Replace it with any `.dcm` or `.dicom` file.

## 1. Write JSON To A File

Linux/macOS:

```bash
dicomcli image.dcm --format json > image.json
```

PowerShell:

```powershell
dicomcli image.dcm --format json | Set-Content image.json -Encoding utf8
```

Use pretty JSON for editing. `--compact` is better for scripts or logs, not manual edits.

## 2. Edit The JSON

Open `image.json` in an editor and change the DICOMweb JSON values you need.

Linux/macOS:

```bash
${EDITOR:-vi} image.json
```

PowerShell:

```powershell
notepad image.json
```

Common editable fields:

```json
{
  "00100010": {
    "vr": "PN",
    "Value": [
      {
        "Alphabetic": "EXAMPLE^PATIENT"
      }
    ]
  },
  "00100020": {
    "vr": "LO",
    "Value": [
      "EXAMPLE-ID"
    ]
  },
  "00081030": {
    "vr": "LO",
    "Value": [
      "Example Study"
    ]
  }
}
```

DICOMweb JSON tag keys use eight hex characters without parentheses or commas. For example, text tag `(0010,0010)` is JSON key `00100010`.

Keep each attribute's `vr` unless you know the replacement value uses a different DICOM value representation. Do not remove required image data unless you intentionally want a header-only or invalid output file.

## 3. Turn JSON Back Into DICOM

Linux/macOS:

```bash
dicomcli image.json --output edited.dcm
```

PowerShell:

```powershell
dicomcli image.json --output edited.dcm
```

Use `--force` only when you intend to overwrite an existing output file:

```bash
dicomcli image.json --output edited.dcm --force
```

## 4. Check The Result

Inspect edited values before sharing or using the new file.

Linux/macOS:

```bash
dicomcli edited.dcm | grep -Ei "Patient|Study|Series|Modality"
```

PowerShell:

```powershell
dicomcli edited.dcm | Select-String "Patient|Study|Series|Modality"
```

## Write Mode Notes

Write mode expects a `.json` input file and a `.dcm` or `.dicom` output file. `--format`, `--binary-format`, and `--compact` are read-output options and cannot be used with `-o` or `--output`.

## Privacy

JSON exported from DICOM can contain protected health information (PHI) and base64-encoded pixel data. Treat both `image.json` and `edited.dcm` as sensitive unless verified de-identified.
