' Double-click to start Unit Progress Tracker without a visible terminal window.
Option Explicit

Dim shell, fso, appDir, nodeModules, cmd

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
appDir = fso.GetParentFolderName(WScript.ScriptFullName)
nodeModules = appDir & "\node_modules"

If Not fso.FolderExists(nodeModules) Then
  MsgBox "node_modules not found." & vbCrLf & vbCrLf & _
         "Open PowerShell in this folder and run:" & vbCrLf & _
         "  npm install" & vbCrLf & _
         "  pip install pywin32" & vbCrLf & vbCrLf & _
         appDir, vbExclamation, "Unit Progress Tracker"
  WScript.Quit 1
End If

shell.CurrentDirectory = appDir
cmd = "cmd /c npm start"
' 0 = hidden window, False = do not wait for exit
shell.Run cmd, 0, False
