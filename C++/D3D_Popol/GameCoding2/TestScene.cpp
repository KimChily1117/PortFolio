#include "pch.h"
#include "TestScene.h"
#include "GeometryHelper.h"
#include "Camera.h"
#include "GameObject.h"
#include "CameraScript.h"
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
			if(pickObj->_name == "Collision")
				CUR_SCENE->Remove(pickObj);
		}
	}

}

void TestScene::InitializeObject()
{
	_shader = make_shared<Shader>(L"23. RenderDemo.fx");

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
		camera->AddComponent(make_shared<CameraScript>());
		Add(camera);
	}

	// UI_Camera
	{
		auto camera = make_shared<GameObject>();
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


	// Mesh
	// Material
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

	// Mesh
	{
		auto obj = make_shared<GameObject>("UI Button");
		obj->AddComponent(make_shared<Button>());

		obj->GetButton()->Create(Vec2(400, 400), Vec2(100, 100), RESOURCES->Get<Material>(L"Veigar"));
		obj->GetButton()->AddOnClickedEvent([obj]() { CUR_SCENE->Remove(obj); });
		CUR_SCENE->Add(obj);
	}

	{
		auto obj = make_shared<GameObject>("Collision");
		obj->GetOrAddTransform()->SetLocalPosition(Vec3(8,5,8));
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
		// Çù°î
		shared_ptr<class Model> m2 = make_shared<Model>();
		m2->ReadModel(L"sr/sr");
		m2->ReadMaterial(L"sr/sr");

		auto obj = make_shared<GameObject>("Rift");
		//obj->GetOrAddTransform()->SetPosition(Vec3(0,0,0));
		obj->GetOrAddTransform()->SetScale(Vec3(0.001f));
		obj->GetOrAddTransform()->SetRotation(Vec3(0.f, XMConvertToRadians(90.f),0.f));

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
		obj->GetOrAddTransform()->SetRotation(Vec3(0.f,0.f,0.f));

		obj->GetTerrain()->Create(145,145, RESOURCES->Get<Material>(L"Veigar"));
		obj->GetMeshRenderer()->SetPass(3);
		CUR_SCENE->Add(obj);
	}	
	
	{
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
		Add(obj);
	}
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
