#include "pch.h"
#include "TestScene.h"
#include "GeometryHelper.h"
#include "Camera.h"
#include "GameObject.h"
#include "EditCameraScript.h"
#include "MeshRenderer.h"
#include "Mesh.h"
#include "Material.h"
#include "Model.h"
#include "ModelRenderer.h"
#include "ModelAnimator.h"
#include "Mesh.h"
#include "Transform.h"
#include "VertexBuffer.h"
#include "IndexBuffer.h"
#include "Light.h"
#include "PlayerController.h"
#include "Terrain.h"
#include "SphereCollider.h"
#include "Button.h"
#include "CameraController.h"
#include "HUDController.h"
#include "SkillIndicatorController.h"
#include "CursorController.h"
#include "ParticleRenderer.h"




void TestScene::Start()
{
	Super::Start();
}

void TestScene::LateUpdate()
{
	Super::LateUpdate();
}
void TestScene::Update()
{
	Super::Update();


	if (INPUT->GetButtonDown(KEY_TYPE::LBUTTON))
	{
		int32 mouseX = INPUT->GetMousePos().x;
		int32 mouseY = INPUT->GetMousePos().y;

		Vec3 Pos;
		// Picking ( Test )
		auto pickObj = CUR_SCENE->Pick(mouseX, mouseY);
		if (pickObj)
		{
			DEBUG_LOG("PIck Obj : Attack Test : " << pickObj->_name.c_str());

			if (pickObj->_name == "Collision")
				CUR_SCENE->Remove(pickObj);
		}
	}

	// Cursor 여기서 정의
	::ShowCursor(FALSE); // Ingame에서 마우스 커서 없애고 자체 MouseCursor Class 정의해서 만듬

	float width = GRAPHICS->GetViewport().GetWidth();
	float height = GRAPHICS->GetViewport().GetHeight();

	// 1️⃣ 마우스 좌표 가져오기 (스크린 기준)
	auto p = INPUT->GetMousePos();

	// 2️⃣ 마우스 좌표를 화면 정규화 좌표(NDC)로 변환
	float ndcX = (p.x / width) * 2.0f - 1.0f; // NDC 기준 -1 ~ 1
	float ndcY = 1.0f - (p.y / height) * 2.0f; // Y축 반전 필요

	// 3️⃣ 화면상의 NDC 좌표를 월드 좌표로 변환 (뷰포트 보정)
	Vec3 cursorPos = Vec3(ndcX * (width / 2), ndcY * (height / 2), 0);


	// ✅ 최종 커서 위치 적용
	_cursor->GetOrAddTransform()->SetPosition(cursorPos);

}

void TestScene::InitializeObject()
{
	_shader = make_shared<Shader>(L"23. RenderDemo.fx");

	// SFX + BGM
	{
		SOUND->LoadSound("BGM", "..\\Resources\\Musics\\Sr_BGM.mp3", true);
		
		
		
		// ANNIE(Voice)
		SOUND->LoadSound("VO_Annie_walk1", "..\\Resources\\Musics\\Annie\\vo_walk1.mp3", false);
		SOUND->LoadSound("VO_Annie_walk2", "..\\Resources\\Musics\\Annie\\vo_walk2.mp3", false);
		SOUND->LoadSound("VO_Annie_walk3", "..\\Resources\\Musics\\Annie\\vo_walk3.mp3", false);
		SOUND->LoadSound("VO_Annie_walk4", "..\\Resources\\Musics\\Annie\\vo_walk1.mp3", false);

		// ANNIE(SFX)

		SOUND->LoadSound("SFX_Annie_GenATK", "..\\Resources\\Musics\\Annie\\sfx_annie_genatk.mp3", false);
		SOUND->LoadSound("SFX_Annie_QSpell", "..\\Resources\\Musics\\Annie\\sfx_annie_qspell.mp3", false);
		SOUND->LoadSound("SFX_Annie_WSpell", "..\\Resources\\Musics\\Annie\\sfx_annie_wspell.mp3", false);
		//////////////////////////////////////////////////////////////////////////////////

		// GAREN(Voice)
		
		SOUND->LoadSound("VO_Garen_walk1", "..\\Resources\\Musics\\Garen\\vo_garen_walk1.mp3", false);
		SOUND->LoadSound("VO_Garen_walk2", "..\\Resources\\Musics\\Garen\\vo_garen_walk2.mp3", false);
		SOUND->LoadSound("VO_Garen_walk3", "..\\Resources\\Musics\\Garen\\vo_garen_walk3.mp3", false);
		SOUND->LoadSound("VO_Garen_walk4", "..\\Resources\\Musics\\Garen\\vo_garen_walk4.mp3", false);

		SOUND->LoadSound("VO_Garen_QSpell1", "..\\Resources\\Musics\\Garen\\vo_garen_Q1.ogg", false);

		SOUND->LoadSound("VO_Garen_WSpell1", "..\\Resources\\Musics\\Garen\\vo_garen_W1.ogg", false);
		SOUND->LoadSound("VO_Garen_WSpell2", "..\\Resources\\Musics\\Garen\\vo_garen_W2.ogg", false);

		SOUND->LoadSound("VO_Garen_ESpell1", "..\\Resources\\Musics\\Garen\\vo_garen_E1.ogg", false);
		SOUND->LoadSound("VO_Garen_ESpell2", "..\\Resources\\Musics\\Garen\\vo_garen_E2.ogg", false);

		SOUND->LoadSound("VO_Garen_RSpell1", "..\\Resources\\Musics\\Garen\\vo_garen_R1.ogg", false);
		SOUND->LoadSound("VO_Garen_RSpell2", "..\\Resources\\Musics\\Garen\\vo_garen_R2.ogg", false);


		// GAREN(SFX)

		SOUND->LoadSound("SFX_Garen_ESpell", "..\\Resources\\Musics\\Garen\\sfx_garen_E1.ogg", false);
		SOUND->LoadSound("SFX_Garen_GenAtk", "..\\Resources\\Musics\\Garen\\sfx_garen_GenAtk.ogg", false);
		SOUND->LoadSound("SFX_Garen_QSpell", "..\\Resources\\Musics\\Garen\\sfx_garen_Q1.ogg", false);
		//////////////////////////////////////////////////////////////
		SOUND->PlaySound("BGM");
	}

	// VFX
	PARTICLE->Add(L"AnnieW", L"..\\Resources\\Particles\\annie_wspell_New.fx", 1);

	// Main Camera
	{
		auto camera = make_shared<GameObject>("Main_Camera");
		//camera->GetOrAddTransform()->SetPosition(Vec3{13.f, 16.f, -9.5f });
		//camera->GetOrAddTransform()->SetRotation(Vec3(25.f, 33.f, 0.f));
		camera->GetOrAddTransform()->SetPosition(Vec3{ 0.f, 0.f, -5.f });
		camera->GetOrAddTransform()->SetRotation(Vec3(0.f, 0.f, 0.f));

		camera->AddComponent(make_shared<Camera>());
		camera->GetCamera()->SetProjectionType(ProjectionType::Perspective);
		camera->GetCamera()->SetCullingMaskLayerOnOff(Layer_UI, true);
		//camera->AddComponent(make_shared<EditCameraScript>());
		camera->AddComponent(make_shared<CameraController>());
		CUR_SCENE->Add(camera);
		camera->LoadTrasnformData();
	}

	// UI_Camera
	{
		auto camera = make_shared<GameObject>("UI_Camera");
		camera->GetOrAddTransform()->SetPosition(Vec3{ 0.f, 0.f, -5.f });
		camera->AddComponent(make_shared<Camera>());
		camera->GetCamera()->SetProjectionType(ProjectionType::Orthographic);
		camera->GetCamera()->SetNear(1.f);
		camera->GetCamera()->SetFar(100.f);


		camera->GetCamera()->SetCullingMaskAll();
		camera->GetCamera()->SetCullingMaskLayerOnOff(Layer_UI, false);
		CUR_SCENE->Add(camera);
	}

	// Light
	{
		auto light = make_shared<GameObject>("Light");
		light->AddComponent(make_shared<Light>());
		LightDesc lightDesc;
		lightDesc.ambient = Vec4(0.4f);
		lightDesc.diffuse = Vec4(1.f);
		lightDesc.specular = Vec4(0.1f);
		lightDesc.direction = Vec3(1.f, 0.f, 1.f);
		light->GetLight()->SetLightDesc(lightDesc);
		Add(light);
	}


	// Material
	{
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"Veigar", L"..\\Resources\\Textures\\veigar.jpg");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"Veigar", material);
		}


		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"Projectile", L"..\\Resources\\Textures\\Annie\\Particles\\fireball.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"Projectile", material);
		}


		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"Trail", L"..\\Resources\\Textures\\Annie\\Particles\\annie_base_q_mis_trail.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"Trail", material);
		}

		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"GarenE", L"..\\Resources\\Textures\\Garen\\Effect\\garen_base_e_spin_edge.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"GarenE", material);
		}






	}
	//// Mesh
	//{
	//	// UI
	//	auto obj = make_shared<GameObject>("UICanvas");
	//	obj->AddComponent(make_shared<Button>());
	//	obj->GetButton()->Create(Vec2(400, 400), Vec2(100, 100), RESOURCES->Get<Material>(L""));

	//	obj->GetButton()->SetOrder(0);
	//	obj->GetTransform()->SetPosition(Vec3{ 0,0,0 });

	//	CUR_SCENE->Add(obj);



	//	auto obj2 = make_shared<GameObject>("UI_HUD");
	//	obj2->AddComponent(make_shared<Button>());

	//	obj2->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"PlayerHUD"));
	//	obj2->GetTransform()->SetParent(obj->GetOrAddTransform());

	//	obj2->GetButton()->SetOrder(1);
	//	obj2->LoadTrasnformData();

	//	CUR_SCENE->Add(obj2);



	//	auto obj3 = make_shared<GameObject>("UI Panel");
	//	obj3->AddComponent(make_shared<Button>());

	//	obj3->GetButton()->Create(Vec2(0, 0), Vec2(1, 1), RESOURCES->Get<Material>(L"empty_circle"));

	//	obj3->GetButton()->SetOrder(2);
	//	obj3->GetTransform()->SetParent(obj2->GetOrAddTransform());
	//	obj3->GetOrAddScript<HUDController>()->ChampMark = obj3;		
	//	obj3->LoadTrasnformData();

	//	UI->SetHUDControllerGameObject(obj3);
	//	CUR_SCENE->Add(obj3);
	//}



	{
		_cursor = make_shared<GameObject>("Cursor");
		_cursor->AddComponent(make_shared<Button>());
		_cursor->GetButton()->Create(Vec2(0, 0), Vec2(75, 75), RESOURCES->Get<Material>(L"hover_precise"));
		_cursor->GetButton()->SetOrder(10);
		

		UI->SetCursorControllerGameObject(_cursor);

		CUR_SCENE->Add(_cursor);
	}

	{
		auto obj = make_shared<GameObject>("Collision");
		obj->GetOrAddTransform()->SetLocalPosition(Vec3(8, 5, 8));
		obj->AddComponent(make_shared<MeshRenderer>());
		{
			obj->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"Veigar"));
		}

		{
			auto mesh = RESOURCES->Get<Mesh>(L"Sphere");
			obj->GetMeshRenderer()->SetMesh(mesh);
			obj->GetMeshRenderer()->SetPass(0);
		}

		{
			auto collider = make_shared<SphereCollider>();
			collider->SetRadius(0.5f);
			obj->AddComponent(collider);
		}


		CUR_SCENE->Add(obj);
	}

	{	
		/*shared_ptr<class Model> m1 = make_shared<Model>();
		m1->ReadModel(L"Effect/Cursor");
		m1->ReadMaterial(L"Effect/Cursor");

		auto obj = make_shared<GameObject>("ClickEffectTTTTTTTTTTTT");
		obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
		obj->GetOrAddTransform()->SetScale(Vec3(0.01f));
		obj->GetOrAddTransform()->SetPosition(Vec3(6.f, 2.0f, 3.f));
		obj->AddComponent(make_shared<ModelRenderer>(_shader));
		{
			obj->GetModelRenderer()->SetModel(m1);
			obj->GetModelRenderer()->SetPass(0);
		}

		CUR_SCENE->Add(obj);


		{
			shared_ptr<class Model> m1 = make_shared<Model>();
			m1->ReadModel(L"Garen/Effect/ESpell");
			m1->ReadMaterial(L"Garen/Effect/GarenESpell");

			auto obj = make_shared<GameObject>("GarenEffect_test");
			obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
			obj->GetOrAddTransform()->SetScale(Vec3(0.01f));
			obj->GetOrAddTransform()->SetPosition(Vec3(9.f, 2.0f, 3.f));
			obj->AddComponent(make_shared<ModelRenderer>(_shader));
			{
				obj->GetModelRenderer()->SetModel(m1);
				obj->GetModelRenderer()->SetPass(0);
			}

			CUR_SCENE->Add(obj);
		}
*/





		//shared_ptr<class Model> m1 = make_shared<Model>();
		//m1->ReadModel(L"Garen/Garen");
		//m1->ReadMaterial(L"Garen/Garen");
		//m1->ReadAnimation(L"Garen/Espell");
		//m1->ReadAnimation(L"Garen/Run");
		//auto obj = make_shared<GameObject>("Annie");
		//obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
		//obj->GetOrAddTransform()->SetScale(Vec3(0.01));	
		//obj->GetOrAddTransform()->SetPosition(Vec3(6.f, 2.0f, 3.f));
		////obj->AddComponent(make_shared<PlayerController>());
		//obj->AddComponent(make_shared<ModelAnimator>(_shader));
		//{
		//	obj->GetModelAnimator()->SetModel(m1);
		//	obj->GetModelAnimator()->SetPass(2);
		//}

		//CUR_SCENE->Add(obj);




		//shared_ptr<class Model> m1 = make_shared<Model>();
		//m1->ReadModel(L"Annie/Annie");
		//m1->ReadMaterial(L"Annie/Annie");
		//m1->ReadAnimation(L"Annie/Rspell");
		//m1->ReadAnimation(L"Annie/Run");
		//auto obj = make_shared<GameObject>("Annie");
		//obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
		//obj->GetOrAddTransform()->SetScale(Vec3(0.0001));
		//obj->GetOrAddTransform()->SetPosition(Vec3(6.f, 2.0f, 3.f));
		////obj->AddComponent(make_shared<PlayerController>());
		//obj->AddComponent(make_shared<ModelAnimator>(_shader));
		//{
		//	obj->GetModelAnimator()->SetModel(m1);
		//	obj->GetModelAnimator()->SetPass(2);
		//}
		//CUR_SCENE->Add(obj);

	}






	/*for (int32 i = 0; i < 100; i++)
	{
		auto obj = make_shared<GameObject>("");
		obj->GetOrAddTransform()->SetLocalPosition(Vec3(rand() % 100, 0, rand() % 100));
		obj->AddComponent(make_shared<MeshRenderer>());
		{
			obj->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"Veigar"));
		}

		{
			auto mesh = RESOURCES->Get<Mesh>(L"Sphere");
			obj->GetMeshRenderer()->SetMesh(mesh);
			obj->GetMeshRenderer()->SetPass(0);
		}

		Add(obj);
	}*/

	{
		// 협곡
		shared_ptr<class Model> m2 = make_shared<Model>();
		m2->ReadModel(L"sr/sr");
		m2->ReadMaterial(L"sr/sr");

		auto obj = make_shared<GameObject>("Rift");
		//obj->GetOrAddTransform()->SetPosition(Vec3(0,0,0));
		obj->GetOrAddTransform()->SetScale(Vec3(0.001f));
		obj->GetOrAddTransform()->SetRotation(Vec3(0.f, XMConvertToRadians(90.f), 0.f));

		obj->AddComponent(make_shared<ModelRenderer>(_shader));
		obj->GetModelRenderer()->SetModel(m2);
		obj->GetModelRenderer()->SetPass(1);

		Add(obj);
	}

	// Terrain
	{
		auto obj = make_shared<GameObject>("Terrain");
		obj->AddComponent(make_shared<Terrain>());
		obj->GetOrAddTransform()->SetLocalPosition(Vec3(0.f, 2.f, 0.f));
		obj->GetOrAddTransform()->SetRotation(Vec3(0.f, 0.f, 0.f));

		obj->GetTerrain()->Create(145, 145, RESOURCES->Get<Material>(L"Veigar"));
		obj->GetMeshRenderer()->SetPass(3);
		_terrain = obj;
		CUR_SCENE->Add(obj);
	}

	/*{
		shared_ptr<class Model> m1 = make_shared<Model>();
		m1->ReadModel(L"Annie/Annie");
		m1->ReadMaterial(L"Annie/Annie");
		m1->ReadAnimation(L"Annie/Idle");
		m1->ReadAnimation(L"Annie/Run");
		auto obj = make_shared<GameObject>("Annie");
		obj->GetOrAddTransform()->SetRotation(Vec3(XMConvertToRadians(90.f), 0.f, 0.f));
		obj->GetOrAddTransform()->SetScale(Vec3(0.0001f));
		obj->GetOrAddTransform()->SetPosition(Vec3(6.f, 1.76f, 3.f));
		obj->AddComponent(make_shared<PlayerController>());
		obj->AddComponent(make_shared<ModelAnimator>(_shader));
		{
			obj->GetModelAnimator()->SetModel(m1);
			obj->GetModelAnimator()->SetPass(2);
		}
		CUR_SCENE->Add(obj);
		GAMEMANAGER->_myPlayer = obj;
	}*/




#pragma region LegacyCode(Katsujin)
	//{
	//	// Animation
	//	shared_ptr<class Model> m1 = make_shared<Model>();
	//	m1->ReadModel(L"Kachujin/Kachujin");
	//	m1->ReadMaterial(L"Kachujin/Kachujin");
	//	m1->ReadAnimation(L"Kachujin/Idle");
	//	m1->ReadAnimation(L"Kachujin/Run");
	//	m1->ReadAnimation(L"Kachujin/Slash");


	//	auto obj = make_shared<GameObject>("Katsu");
	//	//obj->GetOrAddTransform()->SetLocalPosition(Vec3(6.f,2.f,0.f));
	//	//obj->GetOrAddTransform()->SetRotation(Vec3(0.f,-90.f,0.f));

	//	obj->GetOrAddTransform()->SetLocalPosition(Vec3(6.f, 2.f, 1.0f));
	//	obj->GetOrAddTransform()->SetRotation(Vec3(0.f, 0.f, 0.f));


	//	obj->GetOrAddTransform()->SetScale(Vec3(0.005f));
	//	obj->AddComponent(make_shared<ModelAnimator>(_shader));
	//	obj->AddComponent(make_shared<PlayerController>());
	//	{
	//		obj->GetModelAnimator()->SetModel(m1);
	//		obj->GetModelAnimator()->SetPass(2);
	//	}
	//	Add(obj);
	//}



#pragma endregion


}

