$ErrorActionPreference = 'Stop'

$LocalInstaller = if ([string]::IsNullOrEmpty($PSCommandPath)) { $null } else { Join-Path $PSScriptRoot 'install.ps1' }
if ($null -ne $LocalInstaller -and (Test-Path -LiteralPath $LocalInstaller)) {
    & $LocalInstaller -Prerelease
    return
}

$InstallerUrl = 'https://raw.githubusercontent.com/mackeper/DicomCli/main/scripts/install.ps1'

if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $InstallerScript = (Invoke-WebRequest $InstallerUrl -UseBasicParsing).Content
} else {
    $InstallerScript = (Invoke-WebRequest $InstallerUrl).Content
}

& ([scriptblock]::Create($InstallerScript)) -Prerelease
