[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if (-not $artifactsRoot.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Artifacts path escaped the repository.'
}

if (Test-Path -LiteralPath $artifactsRoot) {
    Remove-Item -LiteralPath $artifactsRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $artifactsRoot | Out-Null

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw '.NET SDK was not found.' }

& $dotnet run --project (Join-Path $repoRoot 'tests\FastCopyPaste.Tests\FastCopyPaste.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }

& $dotnet run --project (Join-Path $repoRoot 'tests\FastCopyPaste.HostSmoke\FastCopyPaste.HostSmoke.csproj') -c Release -- --hotkey-tests
if ($LASTEXITCODE -ne 0) { throw 'Hotkey tests failed.' }

& $dotnet run --project (Join-Path $repoRoot 'tests\FastCopyPaste.HostSmoke\FastCopyPaste.HostSmoke.csproj') -c Release -- --inspect-hotkey-dialog
if ($LASTEXITCODE -ne 0) { throw 'Hotkey dialog layout inspection failed.' }

$publishRoot = Join-Path $artifactsRoot 'publish'
& $dotnet publish (Join-Path $repoRoot 'src\FastCopyPaste.Host\FastCopyPaste.Host.csproj') -c Release -r win-x64 --self-contained true -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'Host publish failed.' }

$bundleRoot = Join-Path $artifactsRoot 'bundle'
$payloadRoot = Join-Path $bundleRoot 'payload'
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishRoot '*') -Destination $payloadRoot -Recurse -Force
Get-ChildItem -LiteralPath $payloadRoot -Filter '*.pdb' -File | Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\Install.ps1') -Destination $bundleRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\Uninstall.ps1') -Destination $bundleRoot -Force
foreach ($readmeName in @('README.md', 'README.en.md', 'README.ja.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $readmeName) -Destination $bundleRoot -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets') -Destination $bundleRoot -Recurse -Force

$zipPath = Join-Path $artifactsRoot 'FastCopyPaste-current-user.zip'
Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Built: $zipPath"
