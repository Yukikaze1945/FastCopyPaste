[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$FastCopyPath
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $FastCopyPath)) { throw "FastCopy not found: $FastCopyPath" }
$testRoot = Join-Path $env:TEMP ('FastCopyPaste.Integration.' + [Guid]::NewGuid().ToString('N'))
$expectedPrefix = [IO.Path]::GetFullPath($env:TEMP) + [IO.Path]::DirectorySeparatorChar
$resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
if (-not $resolvedTestRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe test path: $resolvedTestRoot"
}

try {
    $unicodeToken = ([string][char]0x4e2d) + ([char]0x6587)
    $source = Join-Path $testRoot ("source $unicodeToken")
    $copyTarget = Join-Path $testRoot 'copy-target'
    $moveTarget = Join-Path $testRoot 'move-target'
    New-Item -ItemType Directory -Path $source, $copyTarget, $moveTarget -Force | Out-Null
    $copyFileName = "$unicodeToken file.txt"
    $copyFile = Join-Path $source $copyFileName
    Set-Content -LiteralPath $copyFile -Value 'FastCopy integration' -Encoding UTF8

    $fcpSource = Join-Path (Split-Path -Parent $FastCopyPath) 'fcp.exe'
    if (-not (Test-Path -LiteralPath $fcpSource)) { throw "fcp.exe not found beside FastCopy: $fcpSource" }
    $probeExe = Join-Path $testRoot 'fcp.exe'
    Copy-Item -LiteralPath $fcpSource -Destination $probeExe
    & $probeExe '/cmd=diff' '/no_ui' '/force_close' '/log=FALSE' '/ini=integration.ini' $copyFile "/to=$copyTarget"
    if ($LASTEXITCODE -ne 0) { throw "Copy exited with $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath (Join-Path $copyTarget $copyFileName))) { throw 'Copied file missing.' }

    $moveFile = Join-Path $source 'move item.txt'
    Set-Content -LiteralPath $moveFile -Value 'move' -Encoding UTF8
    & $probeExe '/cmd=move' '/no_ui' '/force_close' '/log=FALSE' '/ini=integration.ini' $moveFile "/to=$moveTarget"
    if ($LASTEXITCODE -ne 0) { throw "Move exited with $LASTEXITCODE" }
    if (Test-Path -LiteralPath $moveFile) { throw 'Move source still exists.' }
    if (-not (Test-Path -LiteralPath (Join-Path $moveTarget 'move item.txt'))) { throw 'Moved file missing.' }

    Write-Host 'PASS isolated FastCopy copy'
    Write-Host 'PASS isolated FastCopy move'
} finally {
    if (Test-Path -LiteralPath $resolvedTestRoot) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
