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
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $bundleRoot -Force

Add-Type -AssemblyName System.Drawing
function New-Logo([int]$size, [string]$path) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(40, 125, 70))
            $fontSize = [Math]::Max(10, [Math]::Floor($size * 0.55))
            $font = [System.Drawing.Font]::new('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            try {
                $format = [System.Drawing.StringFormat]::new()
                try {
                    $format.Alignment = [System.Drawing.StringAlignment]::Center
                    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
                    $graphics.DrawString('F', $font, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(0, 0, $size, $size), $format)
                } finally { $format.Dispose() }
            } finally { $font.Dispose() }
        } finally { $graphics.Dispose() }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
}

New-Logo 50 (Join-Path $assetsRoot 'StoreLogo.png')
New-Logo 44 (Join-Path $assetsRoot 'Square44x44Logo.png')
New-Logo 150 (Join-Path $assetsRoot 'Square150x150Logo.png')

$zipPath = Join-Path $artifactsRoot 'FastCopyPaste-current-user.zip'
Compress-Archive -Path (Join-Path $bundleRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Built: $zipPath"
