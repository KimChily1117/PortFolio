#include "pch.h"
#include "StaticMeshDemo.h"
#include "GeometryHelper.h"
#include "Camera.h"
#include "GameObject.h"
#include "CameraScript.h"
#include "MeshRenderer.h"
#include "Mesh.h"
#include "Material.h"
#include "Model.h"
#include "ModelRenderer.h"

StaticMeshDemo::StaticMeshDemo()
{
}

StaticMeshDemo::~StaticMeshDemo()
{
	_obj->shared_from_this().reset();
}

void StaticMeshDemo::Init()
{
	RESOURCES->Init();
	_shader = make_shared<Shader>(L"15. ModelDemo.fx");

	// Camera
	_camera = make_shared<GameObject>("Camera");
	_camera->GetOrAddTransform()->SetPosition(Vec3{ 0.f, 10.f, -35.f });
	_camera->AddComponent(make_shared<Camera>());
	_camera->AddComponent(make_shared<CameraScript>());

	//CreateTower();
	CreateTank();

	//RENDER->Init(_shader);
}

void StaticMeshDemo::Update()
{
	_camera->Update();
	//RENDER->Update();
	{
		LightDesc lightDesc;
		lightDesc.ambient = Vec4(0.4f);
		lightDesc.diffuse = Vec4(1.f);
		lightDesc.specular = Vec4(0.f);
		lightDesc.direction = Vec3(1.f, 0.f, 1.f);
		//RENDER->PushLightData(lightDesc);
	}

	{
		_obj->Update();
		//_obj1->Update();
	}
}

void StaticMeshDemo::Render()
{
}

void StaticMeshDemo::CreateTower()
{
	// CustomData -> Memory
	shared_ptr<class Model> m1 = make_shared<Model>();
	m1->ReadModel(L"Tower/Tower");
	m1->ReadMaterial(L"Tower/Tower");

	_obj = make_shared<GameObject>();
	_obj->GetOrAddTransform()->SetPosition(Vec3(0, 0, 50));
	_obj->GetOrAddTransform()->SetScale(Vec3(1.0f));

	_obj->AddComponent(make_shared<ModelRenderer>(_shader));
	{
		_obj->GetModelRenderer()->SetModel(m1);
		_obj->GetModelRenderer()->SetPass(1);
	}
}

void StaticMeshDemo::CreateTank()
{
	// CustomData -> Memory
	/*m1->ReadModel(L"Tank/Tank");
	m1->ReadMaterial(L"Tank/Tank");*/

	/*m1->ReadModel(L"Heihachi/Heihachi");
	m1->ReadMaterial(L"Heihachi/Heihachi");*/

	/*shared_ptr<class Model> m1 = make_shared<Model>();
	m1->ReadModel(L"DarkKnight2/DarkKnight2_skin1");
	m1->ReadMaterial(L"DarkKnight2/DarkKnight2_skin1");
	_obj = make_shared<GameObject>();
	_obj->GetOrAddTransform()->SetPosition(Vec3(0, 0, 0));
	_obj->GetOrAddTransform()->SetScale(Vec3(0.01f));

	_obj->AddComponent(make_shared<ModelRenderer>(_shader));
	{
		_obj->GetModelRenderer()->SetModel(m1);
		_obj->GetModelRenderer()->SetPass(0);
	}*/


	shared_ptr<class Model> m1 = make_shared<Model>();
	m1->ReadModel(L"Annie/Goth_Annie");
	m1->ReadMaterial(L"Annie/Goth_Annie");
	_obj = make_shared<GameObject>("Ani");
	_obj->GetOrAddTransform()->SetPosition(Vec3(0,0,0));
	_obj->GetOrAddTransform()->SetScale(Vec3(0.001f));

	_obj->AddComponent(make_shared<ModelRenderer>(_shader));
	{
		_obj->GetModelRenderer()->SetModel(m1);
		_obj->GetModelRenderer()->SetPass(0);
	}



	
	/*shared_ptr<class Model> m2 = make_shared<Model>();
	m2->ReadModel(L"DarkKnight2/DarkKnight2_skin1");
	m2->ReadMaterial(L"DarkKnight2/DarkKnight2_skin1");
	_obj1 = make_shared<GameObject>();
	_obj1->GetOrAddTransform()->SetPosition(Vec3(10, 0, 0));
	_obj1->GetOrAddTransform()->SetScale(Vec3(0.01f));

	_obj1->AddComponent(make_shared<ModelRenderer>(_shader));
	{
		_obj1->GetModelRenderer()->SetModel(m2);
		_obj1->GetModelRenderer()->SetPass(1);
	}	*/
}
