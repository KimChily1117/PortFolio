protoc.exe -I=./ --csharp_out=./ ./Common.proto
IF ERRORLEVEL 1 PAUSE

START ../../../Server/PacketGenerator/bin/PacketGenerator.exe ./Common.proto
XCOPY /Y Common.cs "../../../Server/Server/Packet"
XCOPY /Y ServerPacketManager.cs "../../../Server/Server/Packet"