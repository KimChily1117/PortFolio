pushd %~dp0

protoc.exe -I=./ --cpp_out=./ ./Protocol.proto ./Enum.proto ./Struct.proto

IF ERRORLEVEL 1 PAUSE

XCOPY /Y Protocol.pb.h "../../../GameCoding2"
XCOPY /Y Protocol.pb.cc "../../../GameCoding2"
XCOPY /Y Enum.pb.h "../../../GameCoding2"
XCOPY /Y Enum.pb.cc "../../../GameCoding2"
XCOPY /Y Struct.pb.h "../../../GameCoding2"
XCOPY /Y Struct.pb.cc "../../../GameCoding2"

PAUSE
