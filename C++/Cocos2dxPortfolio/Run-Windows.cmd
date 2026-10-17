@echo off
set "gameDir=%~dp0build-win\bin\AsterIdle\Release"
if not exist "%gameDir%\AsterIdle.exe" (
  echo Build the project first: powershell -File tools\build-windows.ps1
  pause
  exit /b 1
)
start "" /D "%gameDir%" "%gameDir%\AsterIdle.exe"
