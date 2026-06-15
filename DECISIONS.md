# Decisions

Decisions taken and why.

## CLI Boundaries

Keep argument parsing in `ArgumentParser` and command execution in `CommandExecutor`; pass typed commands between them so CLI syntax does not leak into execution logic.

## Archive Formats

Keep release executables inside archives so each download includes `LICENSE` and `README.md`, has one checksum, and preserves metadata such as Linux executable permissions. Use `.tar.gz` for Linux and `.zip` for Windows.

## Checksums

Use one `.sha256` sidecar per release artifact in standard `hash  filename` format so users can verify the exact file they downloaded without parsing a combined checksum list.

Linux uses `.tar.gz` because it is the standard Linux distribution format, preserves the executable bit on `dicomcli`, compresses well, and works with `tar -xzf`.

Windows uses `.zip` because it is the standard Windows distribution format and does not need Unix executable permissions.
