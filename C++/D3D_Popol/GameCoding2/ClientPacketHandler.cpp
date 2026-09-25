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
#include "ProjectileScript.h"
#include "ProjectileVisualPolicy.h"
#include "AnnieWEffect.h"
#include "FloatingDamageController.h"
#include "OtherProjectileScript.h"
#include "ParticleRenderer.h"

namespace
{
	constexpr size_t MaxProjectileVisualPoolSize = 64;
	vector<shared_ptr<GameObject>> g_projectileVisualPool;
	weak_ptr<Scene> g_projectilePoolScene;

	void EnsureProjectilePoolScene()
	{
		auto pooledScene = g_projectilePoolScene.lock();
		if (pooledScene.get() == CUR_SCENE.get())
			return;
		g_projectileVisualPool.clear();
		g_projectilePoolScene = CUR_SCENE;
	}

	shared_ptr<GameObject> AcquireProjectileVisual()
	{
		EnsureProjectilePoolScene();
		if (!g_projectileVisualPool.empty())
		{
			auto object = g_projectileVisualPool.back();
			g_projectileVisualPool.pop_back();
			return object;
		}

		auto object = make_shared<GameObject>("PooledProjectile");
		object->GetOrAddTransform();
		object->GetOrAddScript<ProjectileScript>();
		return object;
	}

	void ReleaseProjectileVisual(const shared_ptr<GameObject>& object)
	{
		if (!object)
			return;
		if (auto script = object->GetScript<ProjectileScript>())
			script->ResetForPool();
		CUR_SCENE->Remove(object);
		EnsureProjectilePoolScene();
		if (g_projectileVisualPool.size() < MaxProjectileVisualPoolSize)
			g_projectileVisualPool.push_back(object);
	}
}
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
	case S_NAVIGATION_INFO:
		Handle_S_NavigationInfo(buffer, len);
		break;
	case S_MOVE_ACCEPTED:
		Handle_S_MoveAccepted(buffer, len);
		break;
	case S_MOVE_REJECTED:
		Handle_S_MoveRejected(buffer, len);
		break;
	case S_MOVEMENT_SNAPSHOT:
		Handle_S_MovementSnapshot(buffer, len);
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


// ?ш린 諛묒뿉 彛??댁젣 ?쒕쾭?먯꽌 諛쏆븘?ㅻ뒗(S_ ?꾩튂?? ?⑦궥?ㅼ뿉 ???Handler?ㅼ쓣 ?깅줉??寃껋엫

void ClientPacketHandler::Handle_S_TEST(BYTE* buffer, int32 len)
{
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_TESTMsg pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	string msg = pkt.message();

	DEBUG_LOG("?쒕쾭?먯꽌 ?섏뼱 ???곗씠??: " << msg.c_str() << " ");
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

	DEBUG_LOG("?쒕쾭?먯꽌 ?섏뼱 ???곗씠???⑦궥紐?: S_EnterGame : " << accountId << " , " << isSuccess);
}

void ClientPacketHandler::Handle_S_MyPlayer(BYTE* buffer, int32 len)
{
	// ???⑦궥 ?뚯떛
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_MyPlayer pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	const Protocol::ObjectInfo& info = pkt.info();

	// ???뚮젅?댁뼱 ?ㅻ툕?앺듃 ?앹꽦
	shared_ptr<GameObject> obj = make_shared<GameObject>(info.name());

	// ??Transform ?ㅼ젙 (?뚯쟾 諛??ш린 議곗젙)
	obj->GetOrAddTransform()->SetPosition(Vec3(info.position().x(),
		2, info.position().z()));
	obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
	obj->GetOrAddTransform()->SetScale(Vec3(0.0001f));

	// ??紐⑤뜽 濡쒕뱶 諛??좊땲硫붿씠??異붽?
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


	
		
	// ???ъ뿉 異붽?
	CUR_SCENE->Add(obj);
	CUR_SCENE->RegisterObject(info.objectid(), obj);

	CUR_SCENE->GetMainCamera()->GetScript<CameraController>()->_target = obj;
	CUR_SCENE->GetMainCamera()->GetScript<CameraController>()->_offset = CUR_SCENE->GetMainCamera()->GetTransform()->GetPosition() -
		obj->GetTransform()->GetPosition(); // 珥덇린 ?ㅽ봽???ㅼ젙

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
	// ???⑦궥 ?뚯떛
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


			// ??以묐났 泥댄겕 (?대? 議댁옱?섎뒗 ObjectId?몄? ?뺤씤)
			if (CUR_SCENE->FindObjectById(info.objectid()) != nullptr)
			{
				continue; // ?대? 議댁옱?섎뒗 ?ㅻ툕?앺듃硫?異붽? ?앹꽦 X
			}

			// ???뚮젅?댁뼱 ?ㅻ툕?앺듃 ?앹꽦
			shared_ptr<GameObject> obj = make_shared<GameObject>(info.name());

			// ??Transform ?ㅼ젙 (?뚯쟾 諛??ш린 議곗젙)
			obj->GetOrAddTransform()->SetPosition(Vec3(info.position().x(),
				info.position().y(), info.position().z()));
			obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
			obj->GetOrAddTransform()->SetScale(Vec3(0.0001f));

			// ??紐⑤뜽 濡쒕뱶 諛??좊땲硫붿씠??異붽?
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

			// ???ъ뿉 異붽?
			CUR_SCENE->Add(obj);
			CUR_SCENE->RegisterObject(info.objectid(), obj);

		}
	}
}

void ClientPacketHandler::Handle_S_RemoveObject(BYTE* buffer, int32 len)
{
	// ???⑦궥 ?뚯떛
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
	// ???⑦궥 ?뚯떛
	PacketHeader* header = (PacketHeader*)buffer;
	uint16 id = header->id;
	uint16 size = header->size;

	Protocol::S_Move pkt;
	pkt.ParseFromArray(&header[1], size - sizeof(PacketHeader));

	// ???뚮젅?댁뼱 李얘린
	const Protocol::ObjectInfo& info = pkt.info();
	auto targetPlayer = CUR_SCENE->FindObjectById(info.objectid());
	if (targetPlayer == nullptr)
		return;

	// ???먭린 ?먯떊?대㈃ 臾댁떆
	if (info.objectid() == GAMEMANAGER->_myPlayer->_playerInfo->objectid())
		return;

	// ??MovementController瑜?李얠븘 ?대룞 紐낅졊 ?ㅽ뻾
	auto movementController = targetPlayer->GetOrAddScript<OtherPlayerController>();
	if (movementController)
	{
		Vec3 newPos(info.position().x(), info.position().y(), info.position().z());
		movementController->SetTargetPosition(newPos);
	}
}


void ClientPacketHandler::Handle_S_NavigationInfo(BYTE* buffer, int32 len)
{
	PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
	Protocol::S_NavigationInfo pkt;
	if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)))
		return;

	if (pkt.formatversion() != 1 || pkt.navigationmapid().empty() || pkt.navigationcontenthash().empty())
	{
		NETWORK->ClearNavigationInfo();
		DEBUG_LOG("[Navigation] Invalid NavigationInfo received.");
		return;
	}

	NETWORK->SetNavigationInfo(pkt.roomid(), pkt.formatversion(), pkt.navigationmapid(), pkt.navigationcontenthash());
	DEBUG_LOG("[Navigation] Room=" << pkt.roomid() << " MapId=" << pkt.navigationmapid().c_str() << " Format=" << pkt.formatversion() << " Hash=" << pkt.navigationcontenthash().c_str());
}

void ClientPacketHandler::Handle_S_MoveAccepted(BYTE* buffer, int32 len)
{
	PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
	Protocol::S_MoveAccepted pkt;
	if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)))
		return;
	if (!NETWORK->TryAcceptMoveResponse(pkt.clientmovesequence(), pkt.navigationmapid(), pkt.navigationcontenthash()))
	{
		DEBUG_LOG("[Movement] Ignored stale or mismatched MoveAccepted sequence=" << pkt.clientmovesequence());
		return;
	}
	if (!GAMEMANAGER->_myPlayer || !pkt.has_accepteddestination())
		return;

	const auto& destination = pkt.accepteddestination();
	GAMEMANAGER->_myPlayer->OnMoveAccepted(
		pkt.servermoveid(),
		Vec3(destination.x(), destination.y(), destination.z()),
		pkt.wasdestinationadjusted());
}

void ClientPacketHandler::Handle_S_MoveRejected(BYTE* buffer, int32 len)
{
	PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
	Protocol::S_MoveRejected pkt;
	if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)))
		return;
	if (!NETWORK->TryAcceptMoveResponse(pkt.clientmovesequence(), pkt.navigationmapid(), pkt.navigationcontenthash()))
	{
		DEBUG_LOG("[Movement] Ignored stale or mismatched MoveRejected sequence=" << pkt.clientmovesequence());
		return;
	}
	if (!GAMEMANAGER->_myPlayer || !pkt.has_serverposition())
		return;

	const auto& serverPosition = pkt.serverposition();
	GAMEMANAGER->_myPlayer->OnMoveRejected(
		Vec3(serverPosition.x(), serverPosition.y(), serverPosition.z()),
		pkt.rejectreason());
}

void ClientPacketHandler::Handle_S_MovementSnapshot(BYTE* buffer, int32 len)
{
	PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
	Protocol::S_MovementSnapshot pkt;
	if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)) || !pkt.has_position())
		return;
	if (!NETWORK->HasNavigationInfo() || pkt.roomid() != NETWORK->GetNavigationRoomId())
		return;

	shared_ptr<BasePlayerController> controller;
	const bool isLocalPlayer = GAMEMANAGER->_myPlayer && GAMEMANAGER->_myPlayer->_playerInfo &&
		GAMEMANAGER->_myPlayer->_playerInfo->objectid() == pkt.objectid();
	if (isLocalPlayer)
	{
		controller = GAMEMANAGER->_myPlayer;
	}
	else
	{
		auto object = CUR_SCENE->FindObjectById(pkt.objectid());
		if (object)
			controller = object->GetScript<BasePlayerController>();
	}
	if (!controller)
		return;

	const auto& position = pkt.position();
	if (!controller->ApplyAuthoritativeSnapshot(
		pkt.servermoveid(), pkt.servertick(),
		Vec3(position.x(), position.y(), position.z()),
		pkt.movementstate()))
	{
		return;
	}

	if (isLocalPlayer)
		GAMEMANAGER->_myPlayer->OnMovementSnapshotState(pkt.movementstate());
}
void ClientPacketHandler::Handle_S_SkillResult(BYTE* buffer, int32 len)
{
    PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
    Protocol::S_SkillResult pkt;
    if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)))
        return;

    const uint64 casterId = pkt.casterid();
    const int32 skillId = pkt.skillid();
    const bool isLocal = GAMEMANAGER->_myPlayer && GAMEMANAGER->_myPlayer->_playerInfo &&
        GAMEMANAGER->_myPlayer->_playerInfo->objectid() == casterId;

    // Existing locally predicted skills keep their current path. Annie W is now
    // server-approved because its cone origin/direction must be authoritative.
    if (isLocal && skillId != (int32)SkillType::WSpell)
        return;

    auto duplicate = g_lastPlayedSkill.find(casterId);
    if (duplicate != g_lastPlayedSkill.end() && duplicate->second == skillId)
        return;

    shared_ptr<BasePlayerController> controller;
    if (isLocal)
    {
        controller = GAMEMANAGER->_myPlayer;
    }
    else
    {
        auto caster = CUR_SCENE->FindObjectById(casterId);
        if (caster)
            controller = caster->GetScript<BasePlayerController>();
    }
    if (!controller)
        return;

    if (pkt.hitobjects_size() > 0)
    {
        auto target = CUR_SCENE->FindObjectById(pkt.hitobjects(0));
        if (target)
            controller->SetTarget(target);
    }

    Vec3 castOrigin = controller->GetTransform()->GetPosition();
    Vec3 castDirection = controller->GetTransform()->GetLook();
    if (pkt.has_castorigin())
        castOrigin = Vec3(pkt.castorigin().x(), pkt.castorigin().y(), pkt.castorigin().z());
    if (pkt.has_castdirection())
        castDirection = Vec3(pkt.castdirection().x(), pkt.castdirection().y(), pkt.castdirection().z());

    g_lastPlayedSkill[casterId] = skillId;
    if (skillId == (int32)SkillType::WSpell && controller->_playerInfo &&
        controller->_playerInfo->champtype() == Protocol::PLAYER_TYPE_ANNIE)
    {
        // Play once on server approval, even if a prior attack animation is
        // still finishing. Both local and remote casts use these exact values.
        AnnieWEffect::Get()->Play(castOrigin, castDirection);
        if (isLocal) SOUND->PlaySound("SFX_Annie_WSpell");
    }
    controller->PlayServerSkillResult(skillId, castOrigin, castDirection);
}

void ClientPacketHandler::Handle_S_ProjectileSpawn(BYTE* buffer, int32 len)
{
	PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
	Protocol::S_ProjectileSpawn pkt;
	if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)))
		return;

	auto casterIt = CUR_SCENE->_players.find(pkt.casterid());
	if (casterIt == CUR_SCENE->_players.end())
		return;
	shared_ptr<GameObject> caster = casterIt->second;
	shared_ptr<GameObject> target;
	auto targetIt = CUR_SCENE->_players.find(pkt.targetid());
	if (targetIt != CUR_SCENE->_players.end())
		target = targetIt->second;

	Vec3 startPos = caster->GetTransform()->GetPosition();
	startPos.y += 1.4f;
	Vec3 endPos(pkt.endpos().x(), pkt.endpos().y() + 1.4f, pkt.endpos().z());
	if (target)
	{
		endPos = target->GetTransform()->GetPosition();
		endPos.y += 1.4f;
	}

	auto duplicateIt = CUR_SCENE->_projectiles.find(pkt.projectileid());
	if (duplicateIt != CUR_SCENE->_projectiles.end())
	{
		DEBUG_LOG("[Projectile] Duplicate spawn replaced. id=" << pkt.projectileid());
		ReleaseProjectileVisual(duplicateIt->second);
		CUR_SCENE->_projectiles.erase(duplicateIt);
	}

	auto projectileObj = AcquireProjectileVisual();
	projectileObj->GetTransform()->SetPosition(startPos);
	CUR_SCENE->Add(projectileObj);
	auto controller = caster->GetScript<BasePlayerController>();
	const int championType = controller && controller->_playerInfo ? controller->_playerInfo->champtype() : -1;
	const bool annieQ = UsesAnnieQEffect(championType, pkt);
	projectileObj->GetScript<ProjectileScript>()->SetTarget(endPos, pkt.speed(), target, annieQ);
	if (championType == Protocol::PLAYER_TYPE_ANNIE && !pkt.has_skillid())
	{
		static bool warned = false;
		if (!warned)
			DEBUG_LOG("[AnnieQ] Server has no projectile skillId; using legacy VFX. Rebuild/restart the updated server.");
		warned = true;
	}
	CUR_SCENE->_projectiles[pkt.projectileid()] = projectileObj;
	DEBUG_LOG("[Projectile] Spawn id=" << pkt.projectileid()
		<< " active=" << CUR_SCENE->_projectiles.size()
		<< " pooled=" << g_projectileVisualPool.size());
}

void ClientPacketHandler::Handle_S_ProjectileHit(BYTE* buffer, int32 len)
{
	PacketHeader* header = reinterpret_cast<PacketHeader*>(buffer);
	Protocol::S_ProjectileHit pkt;
	if (!pkt.ParseFromArray(&header[1], header->size - sizeof(PacketHeader)))
		return;

	auto projectileIt = CUR_SCENE->_projectiles.find(pkt.projectileid());
	if (projectileIt == CUR_SCENE->_projectiles.end())
		return;

	auto projectileObject = projectileIt->second;
	if (auto projectileScript = projectileObject->GetScript<ProjectileScript>())
		projectileScript->RemoveTrail();

	Vec3 impactPosition = projectileObject->GetTransform()->GetPosition();
	auto targetIt = CUR_SCENE->_players.find(pkt.targetid());
	if (targetIt != CUR_SCENE->_players.end())
	{
		impactPosition = targetIt->second->GetTransform()->GetPosition();
		impactPosition.y += 1.2f;
	}
	if (auto projectileScript = projectileObject->GetScript<ProjectileScript>())
		projectileScript->OnHit(impactPosition);
	ReleaseProjectileVisual(projectileObject);
	CUR_SCENE->_projectiles.erase(projectileIt);
	DEBUG_LOG("[Projectile] Hit id=" << pkt.projectileid()
		<< " active=" << CUR_SCENE->_projectiles.size()
		<< " pooled=" << g_projectileVisualPool.size());
}
void ClientPacketHandler::Handle_S_Damage(BYTE* buffer, int32 len)
{
	Protocol::S_Damage pkt;
	if (!pkt.ParseFromArray(&buffer[sizeof(PacketHeader)], len - sizeof(PacketHeader)))
		return;

	const uint64 targetId = pkt.targetid();
	const int32 damage = pkt.damage();
	const int32 remainHp = pkt.remainhp();
	DEBUG_LOG("[Combat] S_Damage target=" << targetId << " damage=" << damage << " remainHp=" << remainHp);
	shared_ptr<GameObject> targetObject;

	if (GAMEMANAGER->_myPlayer && GAMEMANAGER->_myPlayer->_playerInfo &&
		GAMEMANAGER->_myPlayer->_playerInfo->objectid() == targetId)
	{
		GAMEMANAGER->_myPlayer->_playerInfo->set_hp(remainHp);
		targetObject = GAMEMANAGER->_myPlayer->GetGameObject();
	}
	else
	{
		auto targetIt = CUR_SCENE->_players.find(targetId);
		if (targetIt != CUR_SCENE->_players.end())
		{
			targetObject = targetIt->second;
			if (auto controller = targetObject->GetScript<BasePlayerController>())
				if (controller->_playerInfo) controller->_playerInfo->set_hp(remainHp);
		}
	}

	if (targetObject && damage > 0)
		FloatingDamageController::Spawn(targetObject, damage);
	else if (!targetObject)
		DEBUG_LOG("[Combat] Damage target object was not found: " << targetId);
}

