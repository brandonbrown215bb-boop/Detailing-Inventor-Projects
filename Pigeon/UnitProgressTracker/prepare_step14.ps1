[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$PublishDirectory = '.artifacts\publish\win-x64'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$solution = Join-Path $projectRoot 'UnitProgressTracker.sln'
$wpfProject = Join-Path $projectRoot 'src\UnitProgressTracker.Wpf\UnitProgressTracker.Wpf.csproj'
$publishPath = Join-Path $projectRoot $PublishDirectory

Push-Location $projectRoot
try {
    dotnet restore $solution -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }

    dotnet build $solution -c Release --no-restore -warnaserror -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    dotnet test $solution -c Release --no-build --no-restore -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Test suite failed.' }

    dotnet restore $wpfProject -r $RuntimeIdentifier -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Runtime-specific restore failed.' }

    dotnet publish $wpfProject -c Release -r $RuntimeIdentifier --self-contained true --no-restore -p:NuGetAudit=false -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

    $executable = Join-Path $publishPath 'UnitProgressTracker.Wpf.exe'
    $runtimeConfig = Join-Path $publishPath 'UnitProgressTracker.Wpf.runtimeconfig.json'
    if (-not (Test-Path -LiteralPath $executable)) { throw "Missing published executable: $executable" }
    if (-not (Test-Path -LiteralPath $runtimeConfig)) { throw "Missing runtime config: $runtimeConfig" }

    $files = @(Get-ChildItem -LiteralPath $publishPath -File)
    [pscustomobject]@{
        PublishPath = $publishPath
        Executable = $executable
        FileCount = $files.Count
        TotalBytes = ($files | Measure-Object Length -Sum).Sum
        WorkstationSmoke = 'PENDING'
    }
}
finally {
    Pop-Location
}
