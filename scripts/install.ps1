param(
    [switch] $Prerelease
)

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

function Read-JsonUrl {
    param([Parameter(Mandatory = $true)] [string] $Url)

    $Headers = @{ 'User-Agent' = 'DicomCli installer' }
    if ($PSVersionTable.PSVersion.Major -lt 6) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    }

    Invoke-RestMethod $Url -Headers $Headers
}

function Get-LatestPrerelease {
    param([Parameter(Mandatory = $true)] [string] $Repository)

    $ReleasesUrl = "https://api.github.com/repos/$Repository/releases?per_page=100"
    $Release = Read-JsonUrl $ReleasesUrl |
        Where-Object { $_.prerelease -and -not $_.draft } |
        Select-Object -First 1

    if ($null -eq $Release) {
        throw "No pre-release found for $Repository."
    }

    $Release
}

function Get-ReleaseAssetUrl {
    param(
        [Parameter(Mandatory = $true)] $Release,
        [Parameter(Mandatory = $true)] [string] $AssetName
    )

    $Asset = $Release.assets |
        Where-Object { $_.name -eq $AssetName } |
        Select-Object -First 1

    if ($null -eq $Asset) {
        throw "Release $($Release.tag_name) does not contain $AssetName."
    }

    $Asset.browser_download_url
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

$Repository = 'mackeper/DicomCli'
$ArchiveName = 'dicomcli-win-x64.zip'
$ChecksumsName = 'SHA256SUMS'
$InstallDir = Join-Path $env:LOCALAPPDATA 'DicomCli'
$InstallPathEntry = $InstallDir
$DownloadUrl = "https://github.com/$Repository/releases/latest/download/$ArchiveName"
$ChecksumsUrl = "https://github.com/$Repository/releases/latest/download/$ChecksumsName"
$PrereleaseTag = $null

if ($Prerelease) {
    $Release = Get-LatestPrerelease $Repository
    $PrereleaseTag = $Release.tag_name
    $DownloadUrl = Get-ReleaseAssetUrl $Release $ArchiveName
    $ChecksumsUrl = Get-ReleaseAssetUrl $Release $ChecksumsName
}

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
$ZipPath = Join-Path $TempDir $ArchiveName
$ChecksumsPath = Join-Path $TempDir $ChecksumsName

try {
    New-Item -ItemType Directory -Force $TempDir | Out-Null
    Save-Url $DownloadUrl $ZipPath
    Save-Url $ChecksumsUrl $ChecksumsPath

    $ExpectedHash = Get-Content $ChecksumsPath |
        ForEach-Object {
            if ($_ -match "^(?<hash>[A-Fa-f0-9]{64})\s+\*?$([regex]::Escape($ArchiveName))$") {
                $Matches.hash.ToLowerInvariant()
            }
        } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        throw "SHA256SUMS does not contain $ArchiveName."
    }

    $ActualHash = (Get-FileHash $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ActualHash -ne $ExpectedHash) {
        throw "Downloaded $ArchiveName failed SHA256 verification."
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

    if (-not [string]::IsNullOrWhiteSpace($PrereleaseTag)) {
        Write-Output "Installed pre-release: $PrereleaseTag"
    }
    Write-Output "Installed to: $InstallDir"
    Write-Output $PathStatus
    Write-Output 'Restart terminal, then run: dicomcli --version'
} finally {
    Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}
