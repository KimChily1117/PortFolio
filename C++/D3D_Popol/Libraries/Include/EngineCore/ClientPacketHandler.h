#pragma once

// ����ٰ� Proto�� �ִ� Enum���� �ٽ� �����մϴ�.
// C#�� ����ϱ� ����

enum 
{
	C_TEST_MSG = 0,
	S_TEST_MSG = 1
};




// �ڵ� �ڵ�ȭ�� �ϸ� �����ٵ�. �װ� ��Ƴ׿�.
class ClientPacketHandler
{
public:
	static void HandlePacket(ServerSessionRef session, BYTE* buffer, int32 len);


	template<typename T>
	static SendBufferRef MakeSendBuffer(T& pkt, uint16 pktId)
	{
		const uint16 dataSize = static_cast<uint16>(pkt.ByteSizeLong());
		const uint16 packetSize = dataSize + sizeof(PacketHeader);

		SendBufferRef sendBuffer = make_shared<SendBuffer>(packetSize);
		PacketHeader* header = reinterpret_cast<PacketHeader*>(sendBuffer->Buffer());
		header->size = packetSize;
		header->id = pktId;
		assert(pkt.SerializeToArray(&header[1], dataSize));
		sendBuffer->Close(packetSize);

		return sendBuffer;
	}
};

