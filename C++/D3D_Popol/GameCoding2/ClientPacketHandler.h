#pragma once

// 여기다가 Proto에 있는 Enum들을 다시 정의합니다.
// C#과 통신하기 위함

enum Proto
{
	C_TEST_MSG = 0,
	S_TEST_MSG = 1,
	C_MOVE = 2,
	S_OBJECT_UPDATE = 3,
	C_SKILL_CAST = 4,
	S_SKILL_RESULT = 5,
	C_REQUEST_MAP = 6,
	S_UPDATE_MAP = 7,
	C_CHAT_MESSAGE = 8,
	S_CHAT_MESSAGE = 9,
	S_ENTER_GAME = 10,
	S_MY_PLAYER = 11,
	S_ADD_OBJECT = 12,
	S_REMOVE_OBJECT = 13,
	S_MOVE = 14,
	C_ENTER_GAME = 15,
	S_PROJECTILE_SPAWN = 16,
	S_PROJECTILE_HIT = 17,
	S_DAMAGE = 18
};





// 코드 자동화를 하면 좋을텐데. 그게 어렵네요.
class ClientPacketHandler
{
public:
	static void HandlePacket(ServerSessionRef session, BYTE* buffer, int32 len);

	// 받기
	static void Handle_S_TEST(BYTE* buffer, int32 len);
	static void Handle_S_EnterGame(BYTE* buffer, int32 len);
	static void Handle_S_MyPlayer(BYTE* buffer, int32 len);
	static void Handle_S_AddObject(BYTE* buffer, int32 len);
	static void Handle_S_RemoveObject(BYTE* buffer, int32 len);
	static void Handle_S_Move(BYTE* buffer, int32 len);
	static void Handle_S_SkillResult(BYTE* buffer, int32 len);
	static void Handle_S_ProjectileSpawn(BYTE* buffer, int32 len);
	static void Handle_S_ProjectileHit(BYTE* buffer, int32 len);
	static void Handle_S_Damage(BYTE* buffer, int32 len);

	static std::unordered_map<uint64, int32> g_lastPlayedSkill;

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

