$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
dotnet format (Join-Path $RepoRoot 'DicomCli.slnx')
