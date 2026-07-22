[CmdletBinding()]
param(
    [string]$FastCopyPath
)

$ErrorActionPreference = 'Stop'

function Assert-SupportedWindows {
    if (-not [Environment]::Is64BitOperatingSystem) {
        throw 'FastCopy Paste requires a 64-bit edition of Windows.'
    }

    $windowsVersion = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
    $buildNumber = 0
    if (-not [int]::TryParse([string]$windowsVersion.CurrentBuildNumber, [ref]$buildNumber)) {
        throw 'The Windows build number could not be determined.'
    }

    if ($buildNumber -lt 19041) {
        throw "FastCopy Paste requires Windows 10 version 2004 (Build 19041) or later. Current build: $buildNumber."
    }

    Write-Verbose "Supported 64-bit Windows build detected: $buildNumber"
}

Assert-SupportedWindows

$payloadRoot = Join-Path $PSScriptRoot 'payload'
if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'FastCopyPaste.Host.exe'))) {
    throw 'The payload directory is missing. Extract the complete release ZIP before installing.'
}
function Find-FastCopyExecutable([string]$requestedPath) {
    if (-not [string]::IsNullOrWhiteSpace($requestedPath)) {
        $resolved = [IO.Path]::GetFullPath($requestedPath)
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "FastCopy was not found: $resolved"
        }
        return $resolved
    }

    $candidates = [Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($env:FASTCOPY_PATH)) {
        $candidates.Add($env:FASTCOPY_PATH)
    }

    $pathCommand = Get-Command FastCopy.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pathCommand) {
        $candidates.Add($pathCommand.Source)
    }

    foreach ($registryPath in @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\FastCopy.exe',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\App Paths\FastCopy.exe'
    )) {
        $registryKey = Get-Item -LiteralPath $registryPath -ErrorAction SilentlyContinue
        if ($registryKey) {
            $registeredPath = $registryKey.GetValue('')
            if (-not [string]::IsNullOrWhiteSpace($registeredPath)) {
                $candidates.Add($registeredPath)
            }
        }
    }

    foreach ($basePath in @(
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        (Join-Path $env:LOCALAPPDATA 'Programs'),
        $env:LOCALAPPDATA
    )) {
        if (-not [string]::IsNullOrWhiteSpace($basePath)) {
            $candidates.Add((Join-Path $basePath 'FastCopy\FastCopy.exe'))
        }
    }

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }

    try {
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = [Windows.Forms.OpenFileDialog]::new()
        try {
            $dialog.Title = 'Select FastCopy.exe'
            $dialog.Filter = 'FastCopy (FastCopy.exe)|FastCopy.exe|Executable files (*.exe)|*.exe'
            $dialog.CheckFileExists = $true
            if ($dialog.ShowDialog() -eq [Windows.Forms.DialogResult]::OK) {
                return [IO.Path]::GetFullPath($dialog.FileName)
            }
        } finally {
            $dialog.Dispose()
        }
    } catch {
        # Headless installs fall through to the actionable error below.
    }

    throw "FastCopy.exe was not found. Re-run with -FastCopyPath 'D:\Tools\FastCopy\FastCopy.exe'."
}

$FastCopyPath = Find-FastCopyExecutable $FastCopyPath

$programsRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$installRoot = [IO.Path]::GetFullPath((Join-Path $programsRoot 'FastCopyPaste'))
$backupRoot = [IO.Path]::GetFullPath((Join-Path $programsRoot 'FastCopyPaste.backup'))
foreach ($candidate in @($installRoot, $backupRoot)) {
    if (-not $candidate.StartsWith($programsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe install path: $candidate"
    }
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'FastCopyPaste'
$installed = $false

function Stop-InstalledHost([string]$expectedPath) {
    Get-CimInstance Win32_Process -Filter "Name='FastCopyPaste.Host.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -eq $expectedPath } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

function Remove-PackageRegistration {
    Get-AppxPackage -Name 'FastCopyPaste' -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop }
}

try {
    Stop-InstalledHost (Join-Path $installRoot 'FastCopyPaste.Host.exe')
    Remove-PackageRegistration
    Start-Sleep -Milliseconds 500

    if (Test-Path -LiteralPath $backupRoot) {
        Remove-Item -LiteralPath $backupRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $installRoot) {
        Move-Item -LiteralPath $installRoot -Destination $backupRoot
    }

    New-Item -ItemType Directory -Path $installRoot | Out-Null
    Copy-Item -Path (Join-Path $payloadRoot '*') -Destination $installRoot -Recurse -Force

    $manifest = Join-Path $installRoot 'AppxManifest.xml'
    Add-AppxPackage -Register $manifest -ExternalLocation $installRoot

    New-Item -Path $runKey -Force | Out-Null
    $hostExe = Join-Path $installRoot 'FastCopyPaste.Host.exe'
    Set-ItemProperty -Path $runKey -Name $runName -Value ('"' + $hostExe + '" --resident')

    $settingsDirectory = Join-Path $env:LOCALAPPDATA 'FastCopyPaste'
    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    $settingsPath = Join-Path $settingsDirectory 'settings.json'
    $hookEnabled = $true
    $hotkey = [ordered]@{ virtualKey = 86; modifiers = 1 }
    if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
        try {
            $existingSettings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($null -ne $existingSettings.hookEnabled) {
                $hookEnabled = [bool]$existingSettings.hookEnabled
            }
            if ($null -ne $existingSettings.hotkey -and
                $null -ne $existingSettings.hotkey.virtualKey -and
                $null -ne $existingSettings.hotkey.modifiers) {
                $hotkey = [ordered]@{
                    virtualKey = [int]$existingSettings.hotkey.virtualKey
                    modifiers = [int]$existingSettings.hotkey.modifiers
                }
            }
        } catch {
            # Invalid legacy settings are replaced with safe defaults below.
        }
    }
    $settings = [ordered]@{
        fastCopyPath = $FastCopyPath
        hookEnabled = $hookEnabled
        hotkey = $hotkey
    }
    $settings | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

    Start-Process -FilePath $hostExe -ArgumentList '--resident' -WindowStyle Hidden
    $installed = $true

    if (Test-Path -LiteralPath $backupRoot) {
        Remove-Item -LiteralPath $backupRoot -Recurse -Force
    }

    Write-Host 'FastCopy Paste installed for the current user.'
    Write-Host "Install path: $installRoot"
} catch {
    Remove-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue
    Remove-PackageRegistration
    Stop-InstalledHost (Join-Path $installRoot 'FastCopyPaste.Host.exe')
    if (Test-Path -LiteralPath $installRoot) {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $backupRoot) {
        Move-Item -LiteralPath $backupRoot -Destination $installRoot
        $oldManifest = Join-Path $installRoot 'AppxManifest.xml'
        if (Test-Path -LiteralPath $oldManifest) {
            Add-AppxPackage -Register $oldManifest -ExternalLocation $installRoot
            $oldHost = Join-Path $installRoot 'FastCopyPaste.Host.exe'
            Set-ItemProperty -Path $runKey -Name $runName -Value ('"' + $oldHost + '" --resident')
            Start-Process -FilePath $oldHost -ArgumentList '--resident' -WindowStyle Hidden
        }
    }
    throw
}

if (-not $installed) { throw 'Installation did not complete.' }
