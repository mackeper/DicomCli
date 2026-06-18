# Contributing

## Install Pre-Releases

Use this Windows PowerShell command to install the latest pre-release build:

```powershell
irm https://raw.githubusercontent.com/mackeper/DicomCli/main/scripts/install-prerelease.ps1 | iex
```

The installer downloads `dicomcli-win-x64.zip`, verifies it against `SHA256SUMS`, installs to `%LOCALAPPDATA%\DicomCli`, and adds that directory to the user `PATH`.

Restart the terminal after installing, then run:

```powershell
dicomcli --version
```
