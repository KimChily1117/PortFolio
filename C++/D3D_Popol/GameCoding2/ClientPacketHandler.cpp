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
#include "MeshRenderer.h"
#include "ProjectileScript.h"
#include "OtherProjectileScript.h"
#include "ParticleRenderer.h"

std::unordered_map<uint64, int32> ClientPacketHandler::g_lastPlayedSkill;

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
	case S_PROJECTILE_SPAWN:
		Handle_S_ProjectileSpawn(buffer, len);
		break;
	case S_PROJECTILE_HIT:
		Handle_S_ProjectileHit(buffer, len);
		break;
	case S_DAMAGE:
		Handle_S_Damage(buffer, len);
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

		obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));

		obj->GetModelAnimator()->SetModel(model);
		obj->GetModelAnimator()->SetPass(2);


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
		model->ReadAnimation(champ + L"/Idle");
		model->ReadAnimation(champ + L"/Espell");
		model->ReadAnimation(champ + L"/Rspell");
		obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));

		obj->GetModelAnimator()->SetModel(model);
		obj->GetModelAnimator()->SetPass(11);

	}


	
		
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
				obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));

				obj->GetModelAnimator()->SetModel(model);
				obj->GetModelAnimator()->SetPass(2);

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
				model->ReadAnimation(champ + L"/Idle");
				model->ReadAnimation(champ + L"/Espell");
				model->ReadAnimation(champ + L"/Rspell");

				{
					auto collider = make_shared<SphereCollider>();
					collider->SetRadius(210.f);
					obj->AddComponent(collider);
				}

				obj->AddComponent(make_shared<ModelAnimator>(CUR_SCENE->_shader));

				obj->GetModelAnimator()->SetModel(model);
				obj->GetModelAnimator()->SetPass(11);


				break;
			}

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

	// ✅ 내가 보낸 스킬이면 무시
	if (casterId == GAMEMANAGER->_myPlayer->_playerInfo->objectid())
	{
		DEBUG_LOG("[Client] 🔄 Ignore my own SkillResult: " << skillId);
		return;
	}

	// ✅ 중복 방지 처리
	auto iter = g_lastPlayedSkill.find(casterId);
	if (iter != g_lastPlayedSkill.end() && iter->second == skillId)
	{
		DEBUG_LOG("[Client] ⚠️ Duplicate Skill Result Ignored for Caster: " << casterId);
		return;
	}
	g_lastPlayedSkill[casterId] = skillId;

	// ✅ 캐스터 찾아서 스킬 실행
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

	// ✅ 타겟 설정
	if (pkt.hitobjects().size() > 0)
	{
		uint64 targetId = pkt.hitobjects(0);
		auto targetIt = CUR_SCENE->_players.find(targetId);
		if (targetIt != CUR_SCENE->_players.end())
			controller->SetTarget(targetIt->second);
	}

	controller->ProcSkill(skillId);
}



void ClientPacketHandler::Handle_S_ProjectileSpawn(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_ProjectileSpawn pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	uint64 casterId = pkt.casterid();
	uint64 targetId = pkt.targetid();

	auto it = CUR_SCENE->_players.find(casterId);
	if (it == CUR_SCENE->_players.end())
	{
		DEBUG_LOG("[Client] ❌ Projectile Spawn Failed: Caster Not Found");
		return;
	}

	// ✅ 투사체 GameObject 생성
	shared_ptr<GameObject> projectileObj = make_shared<GameObject>("Projectile_" + std::to_string(pkt.projectileid()));





	//// ✅ 위치 설정
	//Vec3 startPos(pkt.startpos().x(), pkt.startpos().y() + 2.f, pkt.startpos().z());
	//projectileObj->GetOrAddTransform()->SetPosition(startPos);

	// ✅ 클라이언트에선 startpos를 무시하고, caster의 실제 위치로 보정
	shared_ptr<GameObject> caster = nullptr;

	auto it2 = CUR_SCENE->_players.find(pkt.casterid());
	if (it2 != CUR_SCENE->_players.end())
		caster = it->second;

	Vec3 startPos;
	if (caster)
	{
		startPos = caster->GetTransform()->GetPosition();
		startPos.y += 1.1f;  // 0.8 ~ 1.2 정도가 보통 적절
	}
	else
	{
		startPos = Vec3(pkt.startpos().x(), pkt.startpos().y() + 1.1f, pkt.startpos().z()); // fallback
		startPos.y += 2.f;
	}
	projectileObj->GetOrAddTransform()->SetPosition(startPos);


	// ✅ 메시 렌더러 및 재질 설정 (Sphere Mesh + Veigar Material)
	projectileObj->AddComponent(make_shared<MeshRenderer>());
	{
		auto material = RESOURCES->Get<Material>(L"Projectile");
		auto mesh = RESOURCES->Get<Mesh>(L"Sphere");

		projectileObj->GetMeshRenderer()->SetMaterial(material);
		projectileObj->GetMeshRenderer()->SetMesh(mesh);
		projectileObj->GetMeshRenderer()->SetPass(0); // 기본 패스
		projectileObj->GetTransform()->SetScale(Vec3(0.5f));
	}

	// ✅ 충돌용 콜라이더 추가 (옵션)
	{
		auto collider = make_shared<SphereCollider>();
		collider->SetRadius(0.5f);
		projectileObj->AddComponent(collider);
	}


	if (GAMEMANAGER->_myPlayer->_playerInfo->objectid() == casterId)
	{
		// ✅ 추후 투사체 이동을 위한 Script 컴포넌트 추가 (ProjectileScript 등)
		projectileObj->GetOrAddScript<ProjectileScript>();
		projectileObj->GetScript<ProjectileScript>()->SetTarget(Vec3(pkt.endpos().x(), pkt.endpos().y() + 2.f, pkt.endpos().z()), pkt.speed());
	}

	else
	{
		// ✅ 추후 투사체 이동을 위한 Script 컴포넌트 추가 (ProjectileScript 등)
		projectileObj->GetOrAddScript<OtherProjectileScript>();
		projectileObj->GetScript<OtherProjectileScript>()->SetTarget(Vec3(pkt.endpos().x(), pkt.endpos().y() + 2.f, pkt.endpos().z()), pkt.speed());
	}


	// ✅ 파티클 재생 (위치 기준: 시작 지점)
	Vec3 fxPos = projectileObj->GetTransform()->GetPosition();
	Vec3 fxRot = projectileObj->GetTransform()->GetRotation();
	
	// ✅ 씬에 추가
	CUR_SCENE->Add(projectileObj);
	CUR_SCENE->_projectiles[pkt.projectileid()] = projectileObj;
}


void ClientPacketHandler::Handle_S_ProjectileHit(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_ProjectileHit pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	uint64 projectileId = pkt.projectileid();
	uint64 targetId = pkt.targetid();

	DEBUG_LOG("[Client] 🎯 Projectile Hit Received - ID: " << projectileId << ", Target: " << targetId);

	auto it = CUR_SCENE->_projectiles.find(projectileId);
	if (it != CUR_SCENE->_projectiles.end())
	{
		shared_ptr<GameObject> projObj = it->second;

		// 💥 이펙트 또는 파괴 연출 시작 가능
		// 예: 애니메이션 재생, 파티클 실행 등

	// 1. projObj에서 투사체 스크립트를 얻는다
		auto projectileScript = projObj->GetScript<ProjectileScript>();
		if (!projectileScript)
		{
			auto otherProjectileScript = projObj->GetScript<OtherProjectileScript>();

			auto scriptObj = otherProjectileScript->_trailObj;
			CUR_SCENE->Remove(scriptObj);
		}
		else
		{
			auto scriptObj = projectileScript->_trailObj;
			CUR_SCENE->Remove(scriptObj);
		}
	
		// 🧹 정리
		CUR_SCENE->Remove(projObj);
		CUR_SCENE->_projectiles.erase(projectileId);
	}
}

void ClientPacketHandler::Handle_S_Damage(BYTE* buffer, int32 len)
{
	Protocol::S_Damage pkt;
	pkt.ParseFromArray(&buffer[sizeof(PacketHeader)], len - sizeof(PacketHeader));

	uint64 targetId = pkt.targetid();
	int32 damage = pkt.damage();
	int32 remainHp = pkt.remainhp();

	DEBUG_LOG("[Client] S_Damage Received → Target: " << targetId << ", Damage: " << damage << ", RemainHp: " << remainHp);

	// ✅ 내 플레이어면 HUD 갱신
	if (GAMEMANAGER->_myPlayer && GAMEMANAGER->_myPlayer->_playerInfo->objectid() == targetId)
	{
		// ✅ HP 동기화
		GAMEMANAGER->_myPlayer->_playerInfo->set_hp(remainHp);	
	}
}





