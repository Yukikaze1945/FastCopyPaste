[CmdletBinding()]
param(
    [string]$InstalledHost = (Join-Path $env:LOCALAPPDATA 'Programs\FastCopyPaste\FastCopyPaste.Host.exe')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $InstalledHost)) { throw "Installed host not found: $InstalledHost" }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Collections

$testRoot = Join-Path $env:TEMP ('FastCopyPaste.Installed.' + [Guid]::NewGuid().ToString('N'))
$resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
$expectedPrefix = [IO.Path]::GetFullPath($env:TEMP) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedTestRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe test path: $resolvedTestRoot"
}

function Set-FileClipboard([string]$path, [uint32]$dropEffect) {
    $files = [System.Collections.Specialized.StringCollection]::new()
    [void]$files.Add($path)
    $data = [System.Windows.Forms.DataObject]::new()
    $data.SetFileDropList($files)
    $effectBytes = [BitConverter]::GetBytes($dropEffect)
    $effectStream = [IO.MemoryStream]::new($effectBytes)
    $data.SetData('Preferred DropEffect', $effectStream)
    [System.Windows.Forms.Clipboard]::SetDataObject($data, $true)
}

function Wait-ForPath([string]$path, [int]$seconds = 20) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $path) { return }
        Start-Sleep -Milliseconds 200
    }
    throw "Timed out waiting for: $path"
}

try {
    $source = Join-Path $testRoot 'source'
    $copyTarget = Join-Path $testRoot 'copy-target'
    $moveTarget = Join-Path $testRoot 'move-target'
    New-Item -ItemType Directory -Path $source,$copyTarget,$moveTarget -Force | Out-Null

    $copySource = Join-Path $source 'installed-copy.txt'
    Set-Content -LiteralPath $copySource -Value 'copy' -Encoding UTF8
    Set-FileClipboard $copySource 1
    Start-Process -FilePath $InstalledHost -ArgumentList @('--paste-target', $copyTarget) -WindowStyle Hidden -Wait
    Wait-ForPath (Join-Path $copyTarget 'installed-copy.txt')
    if (-not (Test-Path -LiteralPath $copySource)) { throw 'Copy removed its source.' }
    Write-Host 'PASS installed Host copy pipeline'

    $moveSource = Join-Path $source 'installed-move.txt'
    Set-Content -LiteralPath $moveSource -Value 'move' -Encoding UTF8
    Set-FileClipboard $moveSource 2
    Start-Process -FilePath $InstalledHost -ArgumentList @('--paste-target', $moveTarget) -WindowStyle Hidden -Wait
    Wait-ForPath (Join-Path $moveTarget 'installed-move.txt')
    if (Test-Path -LiteralPath $moveSource) { throw 'Move source still exists.' }

    $clipboardDeadline = [DateTime]::UtcNow.AddSeconds(5)
    while ([DateTime]::UtcNow -lt $clipboardDeadline -and [System.Windows.Forms.Clipboard]::ContainsFileDropList()) {
        Start-Sleep -Milliseconds 100
    }
    if ([System.Windows.Forms.Clipboard]::ContainsFileDropList()) { throw 'Move clipboard was not cleared.' }
    Write-Host 'PASS installed Host move pipeline and clipboard clear'
} finally {
    if (Test-Path -LiteralPath $resolvedTestRoot) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
