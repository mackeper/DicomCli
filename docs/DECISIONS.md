# Decisions

Decisions taken and why.

## CLI Boundaries

Keep argument parsing in `ArgumentParser` and command execution in `CommandExecutor`; pass typed commands between them so CLI syntax does not leak into execution logic.

Prefer `dicomcli <input> [options]` over subcommands. Avoid subcommands unless a future workflow cannot fit that shape cleanly.

## Archive Formats

Keep release executables inside archives so each download has one checksum and preserves metadata such as Linux executable permissions. Do not bundle `LICENSE` or `README.md` in archives because GitHub Releases already links the source archive and repository docs. Use `.tar.gz` for Linux and `.zip` for Windows.

## Checksums

Use one `SHA256SUMS` file for GitHub Releases in standard `hash  filename` format so users can verify release downloads from one checksums asset.

Linux uses `.tar.gz` because it is the standard Linux distribution format, preserves the executable bit on `dicomcli`, compresses well, and works with `tar -xzf`.

Windows uses `.zip` because it is the standard Windows distribution format and does not need Unix executable permissions.
