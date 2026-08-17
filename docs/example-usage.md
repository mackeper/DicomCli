# Example Usage

Examples use `image.dcm`. Replace it with any `.dcm` or `.dicom` file.

## Index

- [Filter text output](example-usage-filter.md) - inspect headers, filter by tag or group, and save small text extracts.
- [Edit through JSON](example-usage-edit.md) - write DICOMweb JSON to a file, edit values, and convert JSON back to DICOM.
- [Compare DICOM files](example-usage-compare.md) - compare headers, exact tags, or DICOMweb JSON with standard diff tools.

## Quick Commands

Read a DICOM file as text:

```bash
dicomcli image.dcm
```

Read a DICOM file as DICOMweb JSON:

```bash
dicomcli image.dcm --format json
```

Extract a binary tag as base64, hex, or XML:

```bash
dicomcli image.dcm --extract 7FE00010:base64
dicomcli image.dcm --extract 7FE00010:hex
dicomcli image.dcm --extract 32531000:xml
```

Write a DICOM file from DICOMweb JSON:

```bash
dicomcli input.json --output output.dcm
```

Write best-effort DICOM from incomplete or non-conformant JSON:

```bash
dicomcli partial.json --output output.dcm --skip-validation
```

`--skip-validation` may create non-conformant output. Do not use it for clinical data workflows unless you understand the missing or invalid attributes.

## Privacy

DICOM files often contain protected health information (PHI). Treat text output, JSON output, screenshots, and generated DICOM files as sensitive unless the source is verified de-identified.
