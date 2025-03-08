protoc.exe --proto_path=./ --csharp_out=./ ./Protocol.proto ./Enum.proto ./Struct.proto

IF ERRORLEVEL 1 PAUSE

START ../../../Server/PacketGenerator/bin/PacketGenerator.exe ./Protocol.proto
XCOPY /Y Protocol.cs "../../../../Project_Dawn/Assets/Scripts/04.Network/Packet"
XCOPY /Y Protocol.cs "../../../Server/Server/Packet"
XCOPY /Y Enum.cs "../../../Server/Server/Packet"
XCOPY /Y Struct.cs "../../../Server/Server/Packet"

XCOPY /Y ClientPacketManager.cs "../../../../Project_Dawn/Assets/Scripts/04.Network/Packet"
XCOPY /Y ServerPacketManager.cs "../../../Server/Server/Packet"