$ErrorActionPreference = "Stop"
$ClientId = "{D2F8C4A1-6B3E-4F9D-A871-5E4C2B9D0F3A}"
$root = $PSScriptRoot
$dll = Join-Path $root "pack\VisTog.dll"
$rules = Join-Path $root "pack\vis-tog-rules.json"
if (-not (Test-Path $dll)) { throw "Missing pack\VisTog.dll — run build.bat first." }
if (-not (Test-Path $rules)) { throw "Missing pack\vis-tog-rules.json — run build.bat first." }

$appData = [Environment]::GetFolderPath("ApplicationData")
$autodesk = Join-Path $appData "Autodesk"
$utf8 = New-Object System.Text.UTF8Encoding($false)

Get-ChildItem $autodesk -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "Inventor *" } |
    ForEach-Object {
        $addins = Join-Path $_.FullName "Addins"
        if (-not (Test-Path $addins)) { return }

        $destDll = Join-Path $addins "VisTog.dll"
        Copy-Item $dll $destDll -Force
        Copy-Item $rules (Join-Path $addins "vis-tog-rules.json") -Force

        $settings = Join-Path $root "pack\vistog-ui-settings.json"
        if (Test-Path $settings) {
            Copy-Item $settings (Join-Path $addins "vistog-ui-settings.json") -Force
        }

        $assetsSrc = Join-Path $root "pack\assets"
        if (Test-Path $assetsSrc) {
            $assetsDst = Join-Path $addins "assets"
            New-Item -ItemType Directory -Force -Path $assetsDst | Out-Null
            Copy-Item (Join-Path $assetsSrc "*") $assetsDst -Force
        }

        $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Addin Type="Standard">
  <ClassId>$ClientId</ClassId>
  <ClientId>$ClientId</ClientId>
  <DisplayName>ISG Visibility</DisplayName>
  <Description>Toggle assembly visibility by IPT stock number.</Description>
  <Assembly>$destDll</Assembly>
  <LoadOnStartUp>1</LoadOnStartUp>
  <UserUnloadable>1</UserUnloadable>
  <Hidden>0</Hidden>
  <SupportedSoftwareVersionLessThan>30</SupportedSoftwareVersionLessThan>
  <SupportedSoftwareVersionGreaterThan>23</SupportedSoftwareVersionGreaterThan>
  <DataVersion>1</DataVersion>
  <UserInterfaceVersion>1</UserInterfaceVersion>
</Addin>
"@
        [IO.File]::WriteAllText((Join-Path $addins "VisTog.addin"), $manifest, $utf8)
    }

Write-Host "ISG Visibility installed. Restart Inventor."
