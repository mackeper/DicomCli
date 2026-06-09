$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
dotnet build (Join-Path $RepoRoot 'DicomCli.slnx')
