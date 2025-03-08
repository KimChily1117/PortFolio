#include "pch.h"
#include "ClientPacketHandler.h"
#include "ServerCore/BufferReader.h"
#include "Model.h"
#include "PlayerController.h"
#include "CameraController.h"
#include "OtherPlayerController.h"
#include "ModelAnimator.h"


void ClientPacketHandler::HandlePacket(ServerSessionRef session, BYTE* buffer, int32 len)
{
	BufferReader br(buffer, len);

	PacketHeader header;
	br >> header;

	Proto type = (Proto)header.id;

	switch (type)
	{
	case S_TEST_MSG:
		Handle_S_TEST(buffer, len);
		break;
	case S_OBJECT_UPDATE:
		break;
	case S_SKILL_RESULT:
		break;
	case S_UPDATE_MAP:
		break;
	case S_CHAT_MESSAGE:
		break;
	case S_ENTER_GAME:
		Handle_S_EnterGame(buffer, len);
		break;
	case S_MY_PLAYER:
		Handle_S_MyPlayer(buffer, len);
		break;
	case S_ADD_OBJECT:
		Handle_S_AddObject(buffer, len);
		break;
	case S_REMOVE_OBJECT:
		Handle_S_RemoveObject(buffer, len);
		break;
	case S_MOVE:
		Handle_S_Move(buffer, len);
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

void ClientPacketHandler::Handle_S_EnterGame(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_EnterGame pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	auto accountId = pkt.accountid();
	auto isSuccess = pkt.success();

	DEBUG_LOG("서버에서 넘어 온 데이터 패킷명 : S_EnterGame : " << accountId << " , " << isSuccess);
}

void ClientPacketHandler::Handle_S_MyPlayer(BYTE* buffer, int32 len)
{
    // ✅ 패킷 파싱
    PacketHeader* header = (PacketHeader*)buffer;
    uint16 id = header->id;
    uint16 size = header->size;

    Protocol::S_MyPlayer pkt;
    pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

    const Protocol::ObjectInfo& info = pkt.info();

    // ✅ 플레이어 오브젝트 생성
    shared_ptr<GameObject> obj = make_shared<GameObject>(info.name());

    // ✅ Transform 설정 (회전 및 크기 조정)
    obj->GetOrAddTransform()->SetPosition(Vec3(info.position().x(),
		2, info.position().z()));
    obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
    obj->GetOrAddTransform()->SetScale(Vec3(0.0001f));

    // ✅ 모델 로드 및 애니메이션 추가
    shared_ptr<class Model> model = make_shared<Model>();
 /*   model->ReadModel(L"Annie/Annie");
    model->ReadMaterial(L"Annie/Annie");
    model->ReadAnimation(L"Annie/Idle");
    model->ReadAnimation(L"Annie/Run");
    model->ReadAnimation(L"Annie/Atk1");*/

	model->ReadModel(L"Garen/Garen");
	model->ReadMaterial(L"Garen/Garen");
	model->ReadAnimation(L"Garen/Idle");
	model->ReadAnimation(L"Garen/Run");

    obj->AddComponent(make_shared<PlayerController>());
    obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));
    
    obj->GetModelAnimator()->SetModel(model);
    obj->GetModelAnimator()->SetPass(2);

    // ✅ 씬에 추가
    CUR_SCENE->Add(obj);
    CUR_SCENE->RegisterObject(info.objectid(),obj);
    GAMEMANAGER->_myPlayer = obj;
    GAMEMANAGER->_myPlayerInfo = info;

	CUR_SCENE->GetMainCamera()->GetScript<CameraController>()->_target = obj;
	CUR_SCENE->GetMainCamera()->GetScript<CameraController>()->_offset = CUR_SCENE->GetMainCamera()->GetTransform()->GetPosition() - 
		obj->GetTransform()->GetPosition(); // ✅ 초기 오프셋 설정


	UI->SetTarget(info.champtype());
}

void ClientPacketHandler::Handle_S_AddObject(BYTE* buffer, int32 len)
{
	// ✅ 패킷 파싱
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_AddObject pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	// Objects Setting
	{
		uint64 myPlayerId = GAMEMANAGER->_myPlayerInfo.objectid();
		const int32 size = pkt.objects_size();

		for (int32 i = 0; i < size; i++)
		{
			const Protocol::ObjectInfo& info = pkt.objects(i);


			if (myPlayerId == info.objectid())
				continue;


			// ✅ 중복 체크 (이미 존재하는 ObjectId인지 확인)
			if (CUR_SCENE->FindObjectById(info.objectid()) != nullptr)
			{
				continue; // 이미 존재하는 오브젝트면 추가 생성 X
			}
			
				// ✅ 플레이어 오브젝트 생성
				shared_ptr<GameObject> obj = make_shared<GameObject>(info.name());

				// ✅ Transform 설정 (회전 및 크기 조정)
				obj->GetOrAddTransform()->SetPosition(Vec3(info.position().x(),
					info.position().y(), info.position().z()));
				obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
				obj->GetOrAddTransform()->SetScale(Vec3(0.0001f));

				// ✅ 모델 로드 및 애니메이션 추가
				shared_ptr<class Model> model = make_shared<Model>();
				model->ReadModel(L"Garen/Garen");
				model->ReadMaterial(L"Garen/Garen");
				model->ReadAnimation(L"Garen/Idle");
				model->ReadAnimation(L"Garen/Run");
				obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));

				obj->GetModelAnimator()->SetModel(model);
				obj->GetModelAnimator()->SetPass(2);

				// ✅ 씬에 추가
				CUR_SCENE->Add(obj);
				CUR_SCENE->RegisterObject(info.objectid(), obj);

		}
	}
}

void ClientPacketHandler::Handle_S_RemoveObject(BYTE* buffer, int32 len)
{
	// ✅ 패킷 파싱
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_RemoveObject pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	const int32 Idssize = pkt.ids_size();

	for (int32 i = 0; i < Idssize; i++)
	{
		auto id = pkt.ids(i);
		CUR_SCENE->RemoveObject(id);
	}
}

void ClientPacketHandler::Handle_S_Move(BYTE* buffer, int32 len)
{
	// ✅ 패킷 파싱
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_Move pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	// ✅ 플레이어 찾기
	const Protocol::ObjectInfo& info = pkt.info();
	auto targetPlayer = CUR_SCENE->FindObjectById(info.objectid());
	if (targetPlayer == nullptr)
		return;

	// ✅ 자기 자신이면 무시
	if (info.objectid() == GAMEMANAGER->_myPlayerInfo.objectid())
		return;

	// ✅ MovementController를 찾아 이동 명령 실행
	auto movementController = targetPlayer->GetOrAddScript<OtherPlayerController>();
	if (movementController)
	{
		Vec3 newPos(info.position().x(), info.position().y(), info.position().z());
		movementController->SetTargetPosition(newPos);
	}
}


