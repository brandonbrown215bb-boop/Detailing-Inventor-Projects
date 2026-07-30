Inventor sidecar for Unit Progress Tracker (same COM read as Pigeon).

Primary: Python + pywin32 (bundled script)
  sidecar/inventor-config-read.py

Requires:
  - Autodesk Inventor installed
  - Python 3 with pywin32 (pip install pywin32)

The viewer runs one hidden Inventor session per scan and reads DOCUMENT_CONFIG_JSON
from each 391Z*.iam — exactly like Pigeon_Extractor.py.

Optional fallback: SurfaceMomSidecar.exe (+ DLLs from Ce3 Surface_Config_Editor build)
  dotnet build Ce3\tools\Surface_Config_Editor\sidecar\SurfaceMomSidecar.csproj -c Release
  Copy bin\Release\net48\SurfaceMomSidecar.exe here

Set USV_PYTHON if python is not on PATH.
