# DicomCli

.NET 10 CLI tool that reads DICOM files and prints their dataset tags.

## Commands

- `make run` runs the CLI against the default project.
- `make build` builds the solution.
- `make format` formats the solution.
- `dotnet test` runs the test project.

## Tests

Tests use xUnit v3 with Microsoft Testing Platform, selected in `global.json`.

The project uses MTP instead of VSTest because the local Linux ARM64/proot environment caused VSTest to reject the installed `dotnet` muxer during test host discovery. Diagnostics showed VSTest found `/usr/lib/dotnet`, but architecture detection for the muxer returned empty because the current process resolved through the proot loader. MTP avoids that VSTest-specific muxer validation path and runs correctly with `dotnet test` on .NET 10.
