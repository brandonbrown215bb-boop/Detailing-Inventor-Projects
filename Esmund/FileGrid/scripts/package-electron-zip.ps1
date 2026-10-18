$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$distDir = Join-Path $projectRoot "dist"
$zipName = "FileGrid-Electron-Windows.zip"
$zipPath = Join-Path $distDir $zipName

Push-Location $projectRoot
Write-Host "Building renderer and main..."
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; exit $LASTEXITCODE }

$stage = Join-Path $env:TEMP ("file-grid-pack-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$includePaths = @(
  "src",
  "build",
  "scripts",
  "out",
  "electron.vite.config.ts",
  "package.json",
  "package-lock.json",
  "tsconfig.json",
  "tsconfig.node.json",
  "tsconfig.web.json",
  "Setup-FileGrid.bat",
  "README.md"
)

foreach ($rel in $includePaths) {
  $src = Join-Path $projectRoot $rel
  if (-not (Test-Path $src)) {
    Write-Warning "Skipping missing path: $rel"
    continue
  }
  $dest = Join-Path $stage $rel
  $parent = Split-Path $dest -Parent
  if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
  }
  if (Test-Path $src -PathType Container) {
    Copy-Item $src $dest -Recurse -Force
  } else {
    Copy-Item $src $dest -Force
  }
}

$packReadme = @"
File Grid (Electron) — Windows package
======================================

Requirements: Node.js LTS (https://nodejs.org/)

1. Unzip this folder anywhere (e.g. Documents\FileGrid).
2. Double-click Setup-FileGrid.bat once.
   - Installs npm dependencies
   - Builds the app (if needed)
   - Creates a desktop shortcut with the File Grid icon
3. Launch File Grid from the desktop shortcut.

To update after replacing files: run Setup-FileGrid.bat again.

Developer run (with console logs): open a terminal here and run npm run dev.

"@
Set-Content -Path (Join-Path $stage "PACKAGE-README.txt") -Value $packReadme -Encoding UTF8

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
if (Test-Path $zipPath) {
  Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

Pop-Location
Write-Host ""
Write-Host "Created: $zipPath"
$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "Size: $sizeMb MB"
