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

$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw 'Visual C++ build tools were not found.' }
$msbuild = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
$nativeRoot = Join-Path $artifactsRoot 'native\Release'
& $msbuild (Join-Path $repoRoot 'src\FastCopyPaste.Shell\FastCopyPaste.Shell.vcxproj') /m /p:Configuration=Release /p:Platform=x64 "/p:OutDir=$nativeRoot\"
if ($LASTEXITCODE -ne 0) { throw 'Shell extension build failed.' }

$bundleRoot = Join-Path $artifactsRoot 'bundle'
$payloadRoot = Join-Path $bundleRoot 'payload'
$assetsRoot = Join-Path $payloadRoot 'Assets'
New-Item -ItemType Directory -Path $assetsRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishRoot '*') -Destination $payloadRoot -Recurse -Force
Get-ChildItem -LiteralPath $payloadRoot -Filter '*.pdb' -File | Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $nativeRoot 'FastCopyPaste.Shell.dll') -Destination $payloadRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\AppxManifest.xml') -Destination $payloadRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\Install.ps1') -Destination $bundleRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\Uninstall.ps1') -Destination $bundleRoot -Force
foreach ($readmeName in @('README.md', 'README.en.md', 'README.ja.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $readmeName) -Destination $bundleRoot -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets') -Destination $bundleRoot -Recurse -Force

Add-Type -AssemblyName System.Drawing
function New-LogoVariant([int]$size, [string]$path) {
    $sourcePath = Join-Path $repoRoot 'assets\fastcopy-paste-logo.png'
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Logo source not found: $sourcePath"
    }

    $sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
    try {
        $bitmap = [System.Drawing.Bitmap]::new(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
            } finally { $graphics.Dispose() }
            $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
    } finally { $sourceImage.Dispose() }
}

New-LogoVariant 50 (Join-Path $assetsRoot 'StoreLogo.png')
New-LogoVariant 44 (Join-Path $assetsRoot 'Square44x44Logo.png')
New-LogoVariant 150 (Join-Path $assetsRoot 'Square150x150Logo.png')

$zipPath = Join-Path $artifactsRoot 'FastCopyPaste-current-user.zip'
Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Built: $zipPath"
