@echo off
setlocal
set "ASTER_OPEN_GALLERY=1"
set "ASTER_GALLERY_CAPTURE_DIR="
set "ASTER_GALLERY_ITEM="
if not exist "%~dp0build-win\bin\AsterIdle\Release\AsterIdle.exe" (
  echo Build first: powershell -File tools/build-windows.ps1
  pause
  exit /b 1
)
pushd "%~dp0build-win\bin\AsterIdle\Release"
start "" AsterIdle.exe
popd
