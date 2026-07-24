@echo off
setlocal
set CLIENT_INDEX=1
set LAUNCH_URL=%~1

echo(%LAUNCH_URL% | findstr /i "client=all" >nul && goto launch_all

echo(%LAUNCH_URL% | findstr /i "client=2" >nul && set CLIENT_INDEX=2
echo(%LAUNCH_URL% | findstr /i "client=3" >nul && set CLIENT_INDEX=3
echo(%LAUNCH_URL% | findstr /i "client=4" >nul && set CLIENT_INDEX=4

call :launch_client %CLIENT_INDEX%
exit /b %ERRORLEVEL%

:launch_all
call :launch_client 1
call :launch_client 2
call :launch_client 3
call :launch_client 4
exit /b 0

:launch_client
set CLIENT_INDEX=%~1
set CLIENT_ID=build0%CLIENT_INDEX%
set GAME_DIR=E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn%CLIENT_INDEX%
set GAME_EXE=%GAME_DIR%\Project_Dawn%CLIENT_INDEX%.exe

if not exist "%GAME_EXE%" (
  echo Project Dawn executable not found:
  echo %GAME_EXE%
  pause
  exit /b 1
)

start "" /D "%GAME_DIR%" "%GAME_EXE%" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=%CLIENT_ID%
exit /b 0
