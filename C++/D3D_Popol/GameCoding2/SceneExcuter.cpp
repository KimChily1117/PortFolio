#include "pch.h"
#include "SceneExcuter.h"
#include "TestScene.h"
#include "Material.h"
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
#include "Graphics.h"
SceneExcuter::SceneExcuter()
{
}

SceneExcuter::~SceneExcuter()
{
}

void SceneExcuter::Init()
{
	auto testScene = make_shared<TestScene>();
	SCENE->ChangeScene(testScene);
	testScene->InitializeObject();

	//_shader = make_shared<Shader>(L"23. RenderDemo.fx");

	//// Camera
	//{
	//	auto camera = make_shared<GameObject>("Camera");
	//	camera->GetOrAddTransform()->SetPosition(Vec3{ 0.f, 0.f, -5.f });
	//	camera->AddComponent(make_shared<Camera>());
	//	camera->AddComponent(make_shared<CameraScript>());
	//	CUR_SCENE->Add(camera);
	//}

	//// Light
	//{
	//	auto light = make_shared<GameObject>("Light");
	//	light->AddComponent(make_shared<Light>());
	//	LightDesc lightDesc;
	//	lightDesc.ambient = Vec4(0.4f);
	//	lightDesc.diffuse = Vec4(1.f);
	//	lightDesc.specular = Vec4(0.1f);
	//	lightDesc.direction = Vec3(1.f, 0.f, 1.f);
	//	light->GetLight()->SetLightDesc(lightDesc);
	//	CUR_SCENE->Add(light);
	//}

	//// Material
	//{
	//	shared_ptr<Material> material = make_shared<Material>();
	//	material->SetShader(_shader);
	//	auto texture = RESOURCES->Load<Texture>(L"Veigar", L"..\\Resources\\Textures\\veigar.jpg");
	//	material->SetDiffuseMap(texture);
	//	MaterialDesc& desc = material->GetMaterialDesc();
	//	desc.ambient = Vec4(1.f);
	//	desc.diffuse = Vec4(1.f);
	//	desc.specular = Vec4(1.f);
	//	RESOURCES->Add(L"Veigar", material);
	//}

	//// Mesh
	//for (int32 i = 0; i < 10; i++)
	//{
	//	auto obj = make_shared<GameObject>("Object");
	//	obj->GetOrAddTransform()->SetLocalPosition(Vec3(rand() % 10, 0, rand() % 10));
	//	obj->AddComponent(make_shared<MeshRenderer>());
	//	{
	//		obj->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"Veigar"));
	//	}
	//	{
	//		auto mesh = RESOURCES->Get<Mesh>(L"Cube");
	//		obj->GetMeshRenderer()->SetMesh(mesh);
	//		obj->GetMeshRenderer()->SetPass(0);
	//	}

	//	CUR_SCENE->Add(obj);
	//}
}

void SceneExcuter::Update()
{

}

void SceneExcuter::Render()
{
	
}
