' Launches built File Grid (npm run preview) with no visible console window.
Option Explicit

Dim fso, shell, scriptDir, projectRoot, nodeModules, logPath, exitCode

Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
projectRoot = fso.GetParentFolderName(scriptDir)
nodeModules = projectRoot & "\node_modules"
logPath = scriptDir & "\launch-error.log"

shell.CurrentDirectory = projectRoot

If Not fso.FolderExists(nodeModules) Then
  exitCode = shell.Run("cmd /c npm install >> """ & logPath & """ 2>&1", 0, True)
  If exitCode <> 0 Then
    MsgBox "File Grid: npm install failed. See scripts\launch-error.log", vbCritical, "File Grid"
    WScript.Quit exitCode
  End If
End If

shell.Run "cmd /c npm run preview", 0, False
