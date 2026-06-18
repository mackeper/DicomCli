# Changelog

Only include changes that affect users, such as features, bug fixes, and user-facing documentation. Do not include internal refactoring, build, CI, or test-only changes.

## 0.1.1

- Added `--extract <tag>:base64|hex|xml` to extract binary DICOM tag data.
- Added `-c, --compare <file>` to show DICOM-aware differences between two DICOM files.
- Changed DICOMweb JSON read output to be pretty-printed by default and added `--compact` for one-line JSON.
- Added `--force` to overwrite existing DICOM output files when writing from DICOMweb JSON.
- Renamed the published binary from `cli` to `dicomcli`.
- Added repository-wide product metadata for `DicomCli` assemblies and package builds.
- Added CLI version output with `--version`.
- Added DICOM read output in text and DICOMweb JSON formats.
- Added DICOM write support from DICOMweb JSON input.
