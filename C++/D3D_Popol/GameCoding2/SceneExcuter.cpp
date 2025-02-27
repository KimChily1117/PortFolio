#include "pch.h"
#include "SceneExcuter.h"
#include "TestScene.h"
#include "Material.h"
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
}

void SceneExcuter::Update()
{

}

void SceneExcuter::Render()
{
	
}
