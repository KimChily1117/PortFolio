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


// ���� �ؿ� �� ���� �������� �޾ƿ���(S_ ��ġ��) ��Ŷ�鿡 ���� Handler���� ����� ����

void ClientPacketHandler::Handle_S_TEST(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_TESTMsg pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	string msg = pkt.message();

	DEBUG_LOG("�������� �Ѿ� �� ������ : " << msg << " ");
}
