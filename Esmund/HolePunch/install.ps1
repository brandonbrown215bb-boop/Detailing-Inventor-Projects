$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dll = Join-Path $root "pack\SkinChannelPunch.dll"
if (-not (Test-Path $dll)) { throw "Missing pack\SkinChannelPunch.dll — run build.bat first." }

$appData = [Environment]::GetFolderPath("ApplicationData")
$plugin = Join-Path $appData "Autodesk\ApplicationPlugins\SkinChannelPunch"
New-Item -ItemType Directory -Force -Path $plugin | Out-Null
Copy-Item $dll (Join-Path $plugin "SkinChannelPunch.dll") -Force

foreach ($name in @("pattern-presets.json", "pattern-presets-editor.html")) {
    $src = Join-Path $root "pack\$name"
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $plugin $name) -Force
    }
}

$assetsSrc = Join-Path $root "pack\assets"
if (Test-Path $assetsSrc) {
    $assetsDst = Join-Path $plugin "assets"
    New-Item -ItemType Directory -Force -Path $assetsDst | Out-Null
    Copy-Item (Join-Path $assetsSrc "*") $assetsDst -Force
}

$installed = Join-Path $plugin "SkinChannelPunch.dll"
$manifest = @"
<Addin Type="Standard">
  <ClassId>{A7C4E2B1-9F3D-4A6E-8C1D-2E5F6A8B9C0D}</ClassId>
  <ClientId>{A7C4E2B1-9F3D-4A6E-8C1D-2E5F6A8B9C0D}</ClientId>
  <DisplayName>Hole Punch</DisplayName>
  <Description>Punch vertical skin hole patterns anchored to perimeter channels.</Description>
  <Assembly>$installed</Assembly>
  <FullClassName>SkinChannelPunch.StandardAddInServer</FullClassName>
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
            [IO.File]::WriteAllText((Join-Path $addins "SkinChannelPunch.addin"), $manifest, $utf8)
        }
    }

Write-Host "Hole Punch installed. Restart Inventor."
