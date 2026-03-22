@ECHO OFF
SETLOCAL

REM ==========================================
REM 현재 위치 기준:
REM   Server/Server/PacketProto/GenPackets.bat
REM ==========================================

SET PROTO_DIR=.
SET LOCAL_GEN_DIR=.\Generated
SET SERVER_PACKET_GEN_DIR=..\Packet\Generated
SET GENERATOR_EXE=..\..\..\Server\PacketGenerator\bin\PacketGenerator.exe

REM ==========================================
REM 출력 폴더 생성
REM ==========================================
IF NOT EXIST "%LOCAL_GEN_DIR%" MKDIR "%LOCAL_GEN_DIR%"
IF NOT EXIST "%SERVER_PACKET_GEN_DIR%" MKDIR "%SERVER_PACKET_GEN_DIR%"

REM ==========================================
REM protobuf C# 생성
REM ==========================================
protoc.exe -I="%PROTO_DIR%" --csharp_out="%LOCAL_GEN_DIR%" "%PROTO_DIR%\Common.proto"
IF ERRORLEVEL 1 GOTO FAIL

protoc.exe -I="%PROTO_DIR%" --csharp_out="%LOCAL_GEN_DIR%" "%PROTO_DIR%\Battle.proto"
IF ERRORLEVEL 1 GOTO FAIL

REM ==========================================
REM PacketManager.g.cs / PacketSerializer.g.cs 생성
REM PacketGenerator.exe <protoDir> <outputDir>
REM ==========================================
"%GENERATOR_EXE%" "%PROTO_DIR%" "%LOCAL_GEN_DIR%"
IF ERRORLEVEL 1 GOTO FAIL

REM ==========================================
REM 서버 프로젝트로 복사
REM ==========================================
XCOPY /Y "%LOCAL_GEN_DIR%\Common.cs" "%SERVER_PACKET_GEN_DIR%\"
IF ERRORLEVEL 1 GOTO FAIL

XCOPY /Y "%LOCAL_GEN_DIR%\Battle.cs" "%SERVER_PACKET_GEN_DIR%\"
IF ERRORLEVEL 1 GOTO FAIL

XCOPY /Y "%LOCAL_GEN_DIR%\PacketManager.g.cs" "%SERVER_PACKET_GEN_DIR%\"
IF ERRORLEVEL 1 GOTO FAIL

XCOPY /Y "%LOCAL_GEN_DIR%\PacketSerializer.g.cs" "%SERVER_PACKET_GEN_DIR%\"
IF ERRORLEVEL 1 GOTO FAIL

ECHO.
ECHO ==========================================
ECHO Packet generation completed successfully.
ECHO ==========================================
GOTO END

:FAIL
ECHO.
ECHO ==========================================
ECHO Packet generation failed.
ECHO ==========================================
PAUSE
EXIT /B 1

:END
ENDLOCAL