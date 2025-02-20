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
}

void TestScene::InitializeObject()
{
	_shader = make_shared<Shader>(L"23. RenderDemo.fx");

	// Camera
	{
		auto camera = make_shared<GameObject>("Main_Camera");
		//camera->GetOrAddTransform()->SetPosition(Vec3{13.f, 16.f, -9.5f });
		//camera->GetOrAddTransform()->SetRotation(Vec3(25.f, 33.f, 0.f));
		camera->GetOrAddTransform()->SetPosition(Vec3{0.f, 0.f, -5.f });
		camera->GetOrAddTransform()->SetRotation(Vec3(0.f, 0.f, 0.f));

		camera->AddComponent(make_shared<Camera>());
		camera->AddComponent(make_shared<CameraScript>());
		Add(camera);
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

	//// Animation
	//shared_ptr<class Model> m1 = make_shared<Model>();
	//m1->ReadModel(L"Kachujin/Kachujin");
	//m1->ReadMaterial(L"Kachujin/Kachujin");
	//m1->ReadAnimation(L"Kachujin/Idle");
	//m1->ReadAnimation(L"Kachujin/Run");
	//m1->ReadAnimation(L"Kachujin/Slash");

	//for (int32 i = 0; i < 500; i++)
	//{
	//	auto obj = make_shared<GameObject>("");
	//	obj->GetOrAddTransform()->SetPosition(Vec3(rand() % 100, 0, rand() % 100));
	//	obj->GetOrAddTransform()->SetScale(Vec3(0.01f));
	//	obj->AddComponent(make_shared<ModelAnimator>(_shader));
	//	{
	//		obj->GetModelAnimator()->SetModel(m1);
	//		obj->GetModelAnimator()->SetPass(2);
	//	}
	//	Add(obj);

	//}
	//{
	//	// Model
	//	shared_ptr<class Model> m2 = make_shared<Model>();
	//	m2->ReadModel(L"Tower/Tower");
	//	m2->ReadMaterial(L"Tower/Tower");

	//	for (int32 i = 0; i < 100; i++)
	//	{
	//		auto obj = make_shared<GameObject>("");
	//		obj->GetOrAddTransform()->SetPosition(Vec3(rand() % 100, 0, rand() % 100));
	//		obj->GetOrAddTransform()->SetScale(Vec3(0.01f));

	//		obj->AddComponent(make_shared<ModelRenderer>(_shader));
	//		{
	//			obj->GetModelRenderer()->SetModel(m2);
	//			obj->GetModelRenderer()->SetPass(1);
	//		}

	//		Add(obj);
	//	}
	//}





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

	/*auto obj = make_shared<GameObject>("");
	obj->GetOrAddTransform()->SetLocalPosition(Vec3(0,0,0));
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

	for (int32 i = 0; i < 100; i++)
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
		obj->GetOrAddTransform()->SetRotation(Vec3(0.f,90.f,0.f));

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
		obj->GetMeshRenderer()->SetPass(1);
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
		obj->GetOrAddTransform()->SetPosition(Vec3(6.f, 2.f, 0.f));
		obj->AddComponent(make_shared<PlayerController>());
		obj->AddComponent(make_shared<ModelAnimator>(_shader));
		{
			obj->GetModelAnimator()->SetModel(m1);
			obj->GetModelAnimator()->SetPass(2);
		}
		Add(obj);
	}

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



}
