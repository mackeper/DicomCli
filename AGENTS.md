# DicomCli

.NET 10 CLI tool that reads DICOM files and prints their dataset tags. Uses `fo-dicom` 5.2.6.

## Commands

- `make run` — runs the CLI (sets `DOTNET_GCHeapHardLimit=7C0000000` for 2 GB heap cap)
- `make build` — builds the project
- `make format` — formats the code

## Structure

- `src/cli/` — single project, entry point `src/cli/Program.cs`
- `DicomCli.slnx` — solution file (single project)
- `0002.DCM` — sample DICOM file in repo root

<developer-review-loop>
## Developer/Reviewer Loop

For every code change (one logical unit: feature, fix, or refactor), follow this cycle:

1. **Implement** the change
2. **Validate**: run `make build`, then `make format`
   - If any step fails, fix and re-run before proceeding
3. **Review**: call a subagent via the `task` tool with `subagent_type` set to `Reviewer1` or `Reviewer2`
4. **Act**: apply reviewer suggestions that improve correctness, style, or architecture
   - If no suggestions, skip to step 6
5. **Re-validate**: run `make build` and `make format` again after applying changes
6. **Repeat** steps 3-5 maximum 3 times per change, or until reviewer has no further suggestions
</developer-review-loop>
