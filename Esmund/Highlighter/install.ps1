# Highlighter installer (PowerShell)
# Deletes any previous Highlighter / Cut Highlight install, then installs clean.
#
# Looks for Highlighter.dll in this order:
#   1) -BuildDir (if passed)
#   2) pack\ next to this script
#   3) bin\Release\net48\ next to this script
#   4) same folder as this script
#
# DLL  -> %APPDATA%\Autodesk\ApplicationPlugins\Highlighter\
# .addin -> %APPDATA%\Autodesk\Inventor *\Addins\Highlighter.addin

param(
    [string]$BuildDir,
    [string]$PackDir
)

$ErrorActionPreference = "Stop"

function Write-InstallText([string]$Message, [string]$Color = "White") {
    Write-Host $Message -ForegroundColor $Color
}

function Unblock-PathTree([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object {
            try { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue } catch { }
        }
}

function Write-AddinManifest([string]$Path, [string]$AssemblyPath) {
    $manifestXml = @"
<Addin Type="Standard">
  <ClassId>{C4E8A1F0-2B5D-4C8E-9A1F-6D3B5E7C8A90}</ClassId>
  <ClientId>{C4E8A1F0-2B5D-4C8E-9A1F-6D3B5E7C8A90}</ClientId>
  <DisplayName>Highlighter</DisplayName>
  <Description>Toggle translucent highlight outlines on skins, liners, and floors by type and color.</Description>
  <Assembly>$AssemblyPath</Assembly>
  <FullClassName>Highlighter.StandardAddInServer</FullClassName>
  <LoadOnStartUp>1</LoadOnStartUp>
  <UserUnloadable>1</UserUnloadable>
  <Hidden>0</Hidden>
  <SupportedSoftwareVersionLessThan>30</SupportedSoftwareVersionLessThan>
  <SupportedSoftwareVersionGreaterThan>23</SupportedSoftwareVersionGreaterThan>
</Addin>
"@
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $manifestXml, $utf8NoBom)
}

function Get-InventorAddinDirectories([string]$AppDataRoot) {
    $addinDirs = New-Object System.Collections.Generic.List[string]
    $autodeskRoot = Join-Path $AppDataRoot "Autodesk"
    if (Test-Path $autodeskRoot) {
        Get-ChildItem -Path $autodeskRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^Inventor 20\d\d$' } |
            ForEach-Object {
                $addinsDir = Join-Path $_.FullName "Addins"
                if (-not (Test-Path $addinsDir)) {
                    New-Item -ItemType Directory -Path $addinsDir -Force | Out-Null
                }
                if (Test-Path $addinsDir) { [void]$addinDirs.Add($addinsDir) }
            }
    }
    return @($addinDirs | Sort-Object -Unique)
}

function Resolve-PayloadFolder([string]$Root, [string]$BuildDir, [string]$PackDir) {
    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($PackDir)) {
        [void]$candidates.Add($PackDir)
    }
    if (-not [string]::IsNullOrWhiteSpace($BuildDir)) {
        [void]$candidates.Add($BuildDir)
    }
    [void]$candidates.Add((Join-Path $Root "pack"))
    [void]$candidates.Add((Join-Path $Root "bin\Release\net48"))
    [void]$candidates.Add($Root)

    foreach ($folder in $candidates) {
        if ([string]::IsNullOrWhiteSpace($folder)) { continue }
        $dll = Join-Path $folder "Highlighter.dll"
        if (Test-Path -LiteralPath $dll) {
            return (Resolve-Path -LiteralPath $folder).Path
        }
    }

    throw "Highlighter.dll not found. Run build.bat to create pack\Highlighter.dll."
}

$appData = [System.Environment]::GetFolderPath("ApplicationData")
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($root)) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

Write-InstallText "========================================================"
Write-InstallText " Highlighter install (PowerShell) - wipe old, install new"
Write-InstallText "========================================================"
Write-InstallText "Script folder: $root"

$payload = Resolve-PayloadFolder -Root $root -BuildDir $BuildDir -PackDir $PackDir
$dllPath = Join-Path $payload "Highlighter.dll"
Write-InstallText "Payload DLL:  $dllPath" "Cyan"

# --- wipe old ---
Write-InstallText ""
Write-InstallText "Cleaning previous installs..." "Yellow"

$legacyPlugin = Join-Path $appData "Autodesk\ApplicationPlugins\CutHighlight"
if (Test-Path -LiteralPath $legacyPlugin) {
    Write-InstallText "  Removing $legacyPlugin"
    Remove-Item -LiteralPath $legacyPlugin -Recurse -Force -ErrorAction SilentlyContinue
}

$pluginRoot = Join-Path $appData "Autodesk\ApplicationPlugins\Highlighter"
if (Test-Path -LiteralPath $pluginRoot) {
    Write-InstallText "  Removing $pluginRoot"
    Remove-Item -LiteralPath $pluginRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$addinDirs = Get-InventorAddinDirectories $appData
foreach ($addinDir in $addinDirs) {
    foreach ($name in @(
        "Highlighter.addin",
        "CutHighlight.addin",
        "Highlighter.dll",
        "CutHighlight.dll"
    )) {
        $path = Join-Path $addinDir $name
        if (Test-Path -LiteralPath $path) {
            Write-InstallText "  Removing $path"
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }
}

# --- install new ---
Write-InstallText ""
Write-InstallText "Installing new files..." "Yellow"
New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
Copy-Item -LiteralPath $dllPath -Destination (Join-Path $pluginRoot "Highlighter.dll") -Force
Write-InstallText "  Copied Highlighter.dll -> $pluginRoot"

$pdb = Join-Path $payload "Highlighter.pdb"
if (Test-Path -LiteralPath $pdb) {
    Copy-Item -LiteralPath $pdb -Destination (Join-Path $pluginRoot "Highlighter.pdb") -Force
}

$assetsSrc = Join-Path $payload "assets"
if (Test-Path -LiteralPath $assetsSrc) {
    $assetsDst = Join-Path $pluginRoot "assets"
    New-Item -ItemType Directory -Path $assetsDst -Force | Out-Null
    Copy-Item -Path (Join-Path $assetsSrc "*") -Destination $assetsDst -Force
    Write-InstallText "  Copied assets -> $assetsDst"
}

Unblock-PathTree $pluginRoot

$installedDll = Join-Path $pluginRoot "Highlighter.dll"
if ($addinDirs.Count -eq 0) {
    Write-InstallText "WARNING: No Inventor */Addins folders under AppData\Autodesk." "Yellow"
    Write-InstallText "Create one after installing Inventor, then re-run install.ps1." "Yellow"
}
else {
    Write-InstallText ""
    Write-InstallText "Writing .addin manifests..." "Yellow"
    foreach ($addinDir in $addinDirs) {
        $manifestPath = Join-Path $addinDir "Highlighter.addin"
        Write-AddinManifest $manifestPath $installedDll
        Write-InstallText "  Wrote $manifestPath" "Green"
    }
}

# Convenience copy next to DLL (not loaded by Inventor; for reference / manual copy)
Write-AddinManifest (Join-Path $pluginRoot "Highlighter.addin") $installedDll

$ver = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedDll).FileVersion
Write-InstallText ""
Write-InstallText "Installation completed. Version $ver" "Green"
Write-InstallText "Plugin folder: $pluginRoot"
Write-InstallText "Restart Inventor, then check Tools > Add-Ins for Highlighter."
Write-InstallText "========================================================"
