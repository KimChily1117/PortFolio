#include "pch.h"
#include "ClientPacketHandler.h"
#include "ServerCore/BufferReader.h"
#include "Model.h"
#include "Button.h"
#include "Material.h"

#include "HUDController.h"

#include "PlayerController.h"
#include "OtherPlayerController.h"
#include "CameraController.h"

#include "AnniePlayerController.h"
#include "AnnieOtherPlayerController.h"

#include "GarenPlayerController.h"
#include "GarenOtherPlayerController.h"

#include "ModelAnimator.h"
#include "SphereCollider.h"


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
		Handle_S_SkillResult(buffer, len);
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

	DEBUG_LOG("서버에서 넘어 온 데이터 : " << msg.c_str() << " ");
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

	wstring champ;





	switch (info.champtype())
	{	
	case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_ANNIE:		
		champ = L"Annie";
		obj->AddComponent(make_shared<AnniePlayerController>());
		obj->GetScript<AnniePlayerController>()->_playerInfo = make_shared<Protocol::ObjectInfo>(pkt.info());
		GAMEMANAGER->_myPlayer = obj->GetScript<AnniePlayerController>();
		

		model->ReadModel(champ + L"/" + champ);
		model->ReadMaterial(champ + L"/" + champ);
		model->ReadAnimation(champ + L"/Idle");
		model->ReadAnimation(champ + L"/Run");
		model->ReadAnimation(champ + L"/Atk1");
		model->ReadAnimation(champ + L"/Atk2");

		model->ReadAnimation(champ + L"/Qspell");
		model->ReadAnimation(champ + L"/Wspell");
		model->ReadAnimation(champ + L"/Espell");
		model->ReadAnimation(champ + L"/Rspell");
		
		{
			auto collider = make_shared<SphereCollider>();
			collider->SetRadius(20000.f);
			obj->AddComponent(collider);
		}


		break;
	case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN:
		champ = L"Garen";
		obj->AddComponent(make_shared<GarenPlayerController>());
		obj->GetScript<GarenPlayerController>()->_playerInfo = make_shared<Protocol::ObjectInfo>(pkt.info());
		GAMEMANAGER->_myPlayer = obj->GetScript<GarenPlayerController>();
		obj->GetTransform()->SetScale(Vec3(0.01f));

		model->ReadModel(champ + L"/" + champ);
		model->ReadMaterial(champ + L"/" + champ);
		model->ReadAnimation(champ + L"/Idle");
		model->ReadAnimation(champ + L"/Run");
		model->ReadAnimation(champ + L"/Atk1");
		model->ReadAnimation(champ + L"/Atk2");

		model->ReadAnimation(champ + L"/Qspell");
		//model->ReadAnimation(champ + L"/Wspell");
		model->ReadAnimation(champ + L"/Espell");
		model->ReadAnimation(champ + L"/Rspell");

	}


	obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));

	obj->GetModelAnimator()->SetModel(model);
	obj->GetModelAnimator()->SetPass(2);
		
	// ✅ 씬에 추가
	CUR_SCENE->Add(obj);
	CUR_SCENE->RegisterObject(info.objectid(), obj);

	CUR_SCENE->GetMainCamera()->GetScript<CameraController>()->_target = obj;
	CUR_SCENE->GetMainCamera()->GetScript<CameraController>()->_offset = CUR_SCENE->GetMainCamera()->GetTransform()->GetPosition() -
		obj->GetTransform()->GetPosition(); // 초기 오프셋 설정

	// Mesh
	{
		// UI
		auto obj = make_shared<GameObject>("UICanvas");
		obj->AddComponent(make_shared<Button>());
		obj->GetButton()->Create(Vec2(400, 400), Vec2(100, 100), RESOURCES->Get<Material>(L""));

		obj->GetButton()->SetOrder(0);
		obj->GetTransform()->SetPosition(Vec3{ 0,0,0 });

		CUR_SCENE->Add(obj);



		auto obj2 = make_shared<GameObject>("UI_HUD");
		obj2->AddComponent(make_shared<Button>());

		obj2->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"PlayerHUD"));
		obj2->GetTransform()->SetParent(obj->GetOrAddTransform());

		obj2->GetButton()->SetOrder(1);
		obj2->LoadTrasnformData();

		CUR_SCENE->Add(obj2);



		auto obj3 = make_shared<GameObject>("UI Panel");
		obj3->AddComponent(make_shared<Button>());

		obj3->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"empty_circle"));

		obj3->GetButton()->SetOrder(2);
		obj3->GetTransform()->SetParent(obj2->GetOrAddTransform());
		obj3->GetOrAddScript<HUDController>()->ChampMark = obj3;
		obj3->LoadTrasnformData();

		UI->SetHUDControllerGameObject(obj3);
		CUR_SCENE->Add(obj3);
	}



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
		uint64 myPlayerId = GAMEMANAGER->_myPlayer->_playerInfo->objectid();
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
			
			wstring champ;

			switch (info.champtype())
			{
			case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_ANNIE:
				champ = L"Annie";
				obj->AddComponent(make_shared<AnnieOtherPlayerController>());
				obj->GetScript<AnnieOtherPlayerController>()->_playerInfo = make_shared<Protocol::ObjectInfo>(pkt.objects(i));
				
				model->ReadModel(champ + L"/" + champ);
				model->ReadMaterial(champ + L"/" + champ);
				model->ReadAnimation(champ + L"/Idle");
				model->ReadAnimation(champ + L"/Run");
				model->ReadAnimation(champ + L"/Atk1");
				model->ReadAnimation(champ + L"/Atk2");

				model->ReadAnimation(champ + L"/Qspell");
				model->ReadAnimation(champ + L"/Wspell");
				model->ReadAnimation(champ + L"/Espell");
				model->ReadAnimation(champ + L"/Rspell");

				{
					auto collider = make_shared<SphereCollider>();
					collider->SetRadius(20000.f);
					obj->AddComponent(collider);
				}


				break;
			case Protocol::PLAYER_CHAMPION_TYPE::PLAYER_TYPE_GAREN:
				champ = L"Garen";
				obj->AddComponent(make_shared<GarenOtherPlayerController>());
				
				obj->GetScript<GarenOtherPlayerController>()->_playerInfo = make_shared<Protocol::ObjectInfo>(pkt.objects(i));
				obj->GetOrAddTransform()->SetScale(Vec3(0.01f));

				model->ReadModel(champ + L"/" + champ);
				model->ReadMaterial(champ + L"/" + champ);
				model->ReadAnimation(champ + L"/Idle");
				model->ReadAnimation(champ + L"/Run");
				model->ReadAnimation(champ + L"/Atk1");
				model->ReadAnimation(champ + L"/Atk2");

				model->ReadAnimation(champ + L"/Qspell");
				//model->ReadAnimation(champ + L"/Wspell");
				model->ReadAnimation(champ + L"/Espell");
				model->ReadAnimation(champ + L"/Rspell");

				{
					auto collider = make_shared<SphereCollider>();
					collider->SetRadius(120.f);
					obj->AddComponent(collider);
				}
				break;

			}

		
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
	if (info.objectid() == GAMEMANAGER->_myPlayer->_playerInfo->objectid())
		return;

	// ✅ MovementController를 찾아 이동 명령 실행
	auto movementController = targetPlayer->GetOrAddScript<OtherPlayerController>();
	if (movementController)
	{
		Vec3 newPos(info.position().x(), info.position().y(), info.position().z());
		movementController->SetTargetPosition(newPos);
	}
}

void ClientPacketHandler::Handle_S_SkillResult(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_SkillResult pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	uint64 casterId = pkt.casterid();
	int32 skillId = pkt.skillid();

	DEBUG_LOG("[Client] ✅ Skill Result Received: Caster " << casterId << " used Skill " << skillId);

	auto it = CUR_SCENE->_players.find(casterId);
	if (it == CUR_SCENE->_players.end())
	{
		DEBUG_LOG("[Client] ❌ Caster Not Found in _players!");
		return;
	}

	shared_ptr<GameObject> caster = it->second;
	auto controller = caster->GetScript<BasePlayerController>();

	if (!controller)
	{
		DEBUG_LOG("[Client] ❌ No Valid Controller Found for Caster " << casterId);
		return;
	}

	controller->ProcSkill(skillId);
}





