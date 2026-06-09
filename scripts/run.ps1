$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
dotnet run --project (Join-Path $RepoRoot 'src/cli') -- @args
