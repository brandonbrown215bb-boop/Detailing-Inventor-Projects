$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
dotnet publish "$root\FileGrid.Portable.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o "$root\publish"

Copy-Item "$root\portable.txt" "$root\publish\portable.txt" -Force
Write-Host "Built: $root\publish\FileGrid.exe"
