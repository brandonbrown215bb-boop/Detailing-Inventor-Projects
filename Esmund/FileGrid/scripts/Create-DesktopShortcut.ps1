param(
  [switch]$Preview
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$launcherName = if ($Preview) { "Launch-FileGrid-Preview.vbs" } else { "Launch-FileGrid-Electron.vbs" }
$launcher = Join-Path $projectRoot "scripts\$launcherName"
$icon = Join-Path $projectRoot "build\icon.ico"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "File Grid.lnk"

if (-not (Test-Path $icon)) {
  Write-Host "Generating icon.ico from build/icon.png..."
  Push-Location $projectRoot
  npm run icon
  Pop-Location
}

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $launcher
$shortcut.WorkingDirectory = $projectRoot
$shortcut.IconLocation = "$icon,0"
$shortcut.Description = "File Grid (Electron)"
$shortcut.Save()

Write-Host "Created: $shortcutPath"
