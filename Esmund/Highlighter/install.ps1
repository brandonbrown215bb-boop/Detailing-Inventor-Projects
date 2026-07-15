$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dll = Join-Path $root "pack\Highlighter.dll"
if (-not (Test-Path $dll)) { throw "Missing pack\Highlighter.dll — run build.bat first." }

$appData = [Environment]::GetFolderPath("ApplicationData")
$plugin = Join-Path $appData "Autodesk\ApplicationPlugins\Highlighter"
New-Item -ItemType Directory -Force -Path $plugin | Out-Null
Copy-Item $dll (Join-Path $plugin "Highlighter.dll") -Force

$assetsSrc = Join-Path $root "pack\assets"
if (Test-Path $assetsSrc) {
    $assetsDst = Join-Path $plugin "assets"
    New-Item -ItemType Directory -Force -Path $assetsDst | Out-Null
    Copy-Item (Join-Path $assetsSrc "*") $assetsDst -Force
}

$installed = Join-Path $plugin "Highlighter.dll"
$manifest = @"
<Addin Type="Standard">
  <ClassId>{C4E8A1F0-2B5D-4C8E-9A1F-6D3B5E7C8A90}</ClassId>
  <ClientId>{C4E8A1F0-2B5D-4C8E-9A1F-6D3B5E7C8A90}</ClientId>
  <DisplayName>Highlighter</DisplayName>
  <Description>Toggle translucent highlight outlines on skins, liners, and floors by type and color.</Description>
  <Assembly>$installed</Assembly>
  <FullClassName>Highlighter.StandardAddInServer</FullClassName>
  <LoadOnStartUp>1</LoadOnStartUp>
  <UserUnloadable>1</UserUnloadable>
  <Hidden>0</Hidden>
  <SupportedSoftwareVersionLessThan>30</SupportedSoftwareVersionLessThan>
  <SupportedSoftwareVersionGreaterThan>23</SupportedSoftwareVersionGreaterThan>
</Addin>
"@

$utf8 = New-Object System.Text.UTF8Encoding($false)
$autodesk = Join-Path $appData "Autodesk"
Get-ChildItem $autodesk -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "Inventor *" } |
    ForEach-Object {
        $addins = Join-Path $_.FullName "Addins"
        if (Test-Path $addins) {
            [IO.File]::WriteAllText((Join-Path $addins "Highlighter.addin"), $manifest, $utf8)
        }
    }

Write-Host "Highlighter installed. Restart Inventor."
