# Validation

These commands are evidence-backed starting points. Correct this page when the repository disagrees.

- Inspect the repository and replace this line with verified commands.

## UnitProgressTracker

Run from `Pigeon/UnitProgressTracker`:

```powershell
dotnet restore UnitProgressTracker.sln -p:NuGetAudit=false
dotnet build UnitProgressTracker.sln -c Release --no-restore -warnaserror -p:NuGetAudit=false
dotnet test UnitProgressTracker.sln -c Release --no-build --no-restore -p:NuGetAudit=false
dotnet restore src\UnitProgressTracker.Wpf\UnitProgressTracker.Wpf.csproj -r win-x64 -p:NuGetAudit=false
dotnet publish src\UnitProgressTracker.Wpf\UnitProgressTracker.Wpf.csproj -c Release -r win-x64 --self-contained true --no-restore -p:NuGetAudit=false -o .artifacts\publish\win-x64
```

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare_step14.ps1`
performs the same gate preparation on workstations whose default policy blocks local
scripts. Automated evidence does not
replace the fresh-machine WPF launch or supported Inventor/Apprentice smoke described in
`STEP14_WORKSTATION_SMOKE.md`.
