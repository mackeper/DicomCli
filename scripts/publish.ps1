$ErrorActionPreference = 'Stop'

function Get-DefaultRuntime {
    $os = if ($IsWindows) {
        'win'
    } elseif ($IsLinux) {
        'linux'
    } elseif ($IsMacOS) {
        'osx'
    } else {
        throw 'Unsupported OS.'
    }

    $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64' { 'x64' }
        'Arm64' { 'arm64' }
        default { throw "Unsupported architecture: $_" }
    }

    "$os-$arch"
}

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$Runtime = if ($args.Count -gt 0) { $args[0] } else { Get-DefaultRuntime }
$BinDir = Join-Path $RepoRoot 'bin'

Remove-Item (Join-Path $BinDir 'cli'), (Join-Path $BinDir 'cli.exe'), (Join-Path $BinDir 'dicomcli'), (Join-Path $BinDir 'dicomcli.exe') -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $RepoRoot 'src/cli/cli.csproj') `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:StripSymbols=true `
    -p:InvariantGlobalization=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $BinDir

if ($Runtime.StartsWith('win-')) {
    Write-Output "Run with: $(Join-Path $BinDir 'dicomcli.exe')"
} else {
    Write-Output "Run with: $(Join-Path $BinDir 'dicomcli')"
}
