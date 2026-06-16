$ErrorActionPreference = 'Stop'

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw 'This installer supports Windows only.'
}

function Save-Url {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [string] $OutFile
    )

    if ($PSVersionTable.PSVersion.Major -lt 6) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest $Url -OutFile $OutFile -UseBasicParsing
    } else {
        Invoke-WebRequest $Url -OutFile $OutFile
    }
}

function Get-PathKey {
    param([Parameter(Mandatory = $true)] [string] $Path)

    $ExpandedPath = [Environment]::ExpandEnvironmentVariables($Path.Trim().Trim('"'))
    try {
        $ExpandedPath = [System.IO.Path]::GetFullPath($ExpandedPath)
    } catch {
        # Keep the expanded string if a PATH entry is not a normal file path.
    }

    $ExpandedPath.TrimEnd('\', '/').ToUpperInvariant()
}

$InstallPathEntry = '%LOCALAPPDATA%\DicomCli'
$InstallDir = Join-Path $env:LOCALAPPDATA 'DicomCli'
$DownloadUrl = 'https://github.com/mackeper/DicomCli/releases/latest/download/dicomcli-win-x64.zip'
$ChecksumsUrl = 'https://github.com/mackeper/DicomCli/releases/latest/download/SHA256SUMS'
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
$ZipPath = Join-Path $TempDir 'dicomcli-win-x64.zip'
$ChecksumsPath = Join-Path $TempDir 'SHA256SUMS'

try {
    New-Item -ItemType Directory -Force $TempDir | Out-Null
    Save-Url $DownloadUrl $ZipPath
    Save-Url $ChecksumsUrl $ChecksumsPath

    $ExpectedHash = Get-Content $ChecksumsPath |
        ForEach-Object {
            if ($_ -match '^(?<hash>[A-Fa-f0-9]{64})\s+\*?dicomcli-win-x64\.zip$') {
                $Matches.hash.ToLowerInvariant()
            }
        } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        throw 'SHA256SUMS does not contain dicomcli-win-x64.zip.'
    }

    $ActualHash = (Get-FileHash $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ActualHash -ne $ExpectedHash) {
        throw 'Downloaded dicomcli-win-x64.zip failed SHA256 verification.'
    }

    New-Item -ItemType Directory -Force $InstallDir | Out-Null
    Expand-Archive $ZipPath -DestinationPath $InstallDir -Force

    $UserPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $PathEntries = if ([string]::IsNullOrEmpty($UserPath)) { @() } else { $UserPath -split ';' }
    $InstallPathKey = Get-PathKey $InstallDir
    $NewPathEntries = @()
    $HasInstallPath = $false
    $PathChanged = $false

    foreach ($PathEntry in $PathEntries) {
        if ([string]::IsNullOrWhiteSpace($PathEntry)) {
            $NewPathEntries += $PathEntry
            continue
        }

        $TrimmedPathEntry = $PathEntry.Trim()
        if ((Get-PathKey $TrimmedPathEntry) -eq $InstallPathKey) {
            if (-not $HasInstallPath) {
                $NewPathEntries += $InstallPathEntry
                $HasInstallPath = $true
                if ($TrimmedPathEntry -cne $InstallPathEntry) {
                    $PathChanged = $true
                }
            } else {
                $PathChanged = $true
            }
        } else {
            $NewPathEntries += $PathEntry
        }
    }

    $PathStatus = "User PATH already contains: $InstallPathEntry"
    if (-not $HasInstallPath) {
        $NewPathEntries += $InstallPathEntry
        $PathChanged = $true
        $PathStatus = "Added to user PATH: $InstallPathEntry"
    } elseif ($PathChanged) {
        $PathStatus = "Normalized user PATH entry: $InstallPathEntry"
    }

    if ($PathChanged) {
        [Environment]::SetEnvironmentVariable('Path', ($NewPathEntries -join ';'), 'User')
    }

    Write-Output "Installed to: $InstallDir"
    Write-Output $PathStatus
    Write-Output 'Restart terminal, then run: dicomcli --version'
} finally {
    Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}
