@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GameServers.ps1" %*
exit /b %ERRORLEVEL%
