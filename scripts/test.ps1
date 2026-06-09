$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
dotnet test --solution (Join-Path $RepoRoot 'DicomCli.slnx') --configuration Release
