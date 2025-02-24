#include "pch.h"
#include "ClientPacketHandler.h"
#include "ServerCore/BufferReader.h"


void ClientPacketHandler::HandlePacket(ServerSessionRef session, BYTE* buffer, int32 len)
{
	BufferReader br(buffer, len);

	PacketHeader header;
	br >> header;

	switch (header.id)
	{
	case S_TEST_MSG:
		Handle_S_TEST(buffer, len);
	break;

	default:
		break;
	}
}


// 여기 밑에 쭉 이제 서버에서 받아오는(S_ 전치사) 패킷들에 대한 Handler들을 등록할 것임

void ClientPacketHandler::Handle_S_TEST(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_TESTMsg pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	string msg = pkt.message();

	DEBUG_LOG("서버에서 넘어 온 데이터 : " << msg << " ");
}
