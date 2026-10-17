@echo off
set "ASTER_OPEN_GALLERY="
set "ASTER_GALLERY_CAPTURE_DIR="
set "ASTER_SMOKE_SCENE="
set "ASTER_CAPTURE_DIR="
set "ASTER_FRONTEND_PAGE="
set "ASTER_SMOKE_SAVE_DIR="
cd /d "%~dp0build-win\bin\AsterIdle\Release"
start "" AsterIdle.exe
