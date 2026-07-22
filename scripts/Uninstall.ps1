[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$programsRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$installRoot = [IO.Path]::GetFullPath((Join-Path $programsRoot 'FastCopyPaste'))
$dataRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'FastCopyPaste'))
if (-not $installRoot.StartsWith($programsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe install path: $installRoot"
}
if (-not $dataRoot.StartsWith([IO.Path]::GetFullPath($env:LOCALAPPDATA) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe data path: $dataRoot"
}

$hostExe = Join-Path $installRoot 'FastCopyPaste.Host.exe'
Get-CimInstance Win32_Process -Filter "Name='FastCopyPaste.Host.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -eq $hostExe } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'FastCopyPaste' -ErrorAction SilentlyContinue
Get-AppxPackage -Name 'FastCopyPaste' -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop }
Start-Sleep -Milliseconds 500

if (Test-Path -LiteralPath $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}
if (Test-Path -LiteralPath $dataRoot) {
    Remove-Item -LiteralPath $dataRoot -Recurse -Force
}

Write-Host 'FastCopy Paste was removed for the current user.'

