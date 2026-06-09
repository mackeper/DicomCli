$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$Solution = Join-Path $RepoRoot 'DicomCli.slnx'

dotnet restore $Solution
dotnet format $Solution --verify-no-changes --no-restore
dotnet build $Solution --configuration Release --no-restore
dotnet test --solution $Solution --configuration Release --no-build
