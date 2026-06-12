# Roadmap

## Security

- Add CI dependency vulnerability checking after `v0.1.0`.
- Add SBOM generation after `v0.1.0`.
- Add artifact signing after `v0.1.0` if release audience requires it.

## Chore

- Add GitHub issue templates after `v0.1.0` if public issue intake becomes important.
- Add `linux-arm64` published-binary smoke testing after `v0.1.0`.

## Features

- Add `--pretty`.
- Add `--deidentify`.
- Add de-identification controls: `--remove-private`, `--regenerate-uids`, `--date-shift <days>`, `--keep-pixel-data`.
- Add tag filtering: `--tag <tag-or-keyword>`, `--exclude <tag-or-keyword>`, `--no-pixel-data`, `--no-private`.
- Add `--validate`.
- Add richer CLI exit codes.
- Add strict DICOMweb JSON mode: `--strict`, `--include-names`. Current JSON includes a non-standard `name` field.
- Add BulkData support: `--bulk-data-uri`, `--inline-binary-limit <bytes>`, `--externalize-pixel-data`.
- Add tag editing: `--set <tag-or-keyword=value>`, `--remove <tag-or-keyword>`
- Add pixel/frame tools: `--pixel-summary`, `--extract-frame <index>`, `--decompress`.
- Add stdin/stdout support with `-`.
- Add -c/--compare <file2>
