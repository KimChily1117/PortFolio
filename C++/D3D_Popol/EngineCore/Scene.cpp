#include "pch.h"
#include "Scene.h"
#include "GameObject.h"
#include "BaseCollider.h"
#include "Camera.h"
#include "Terrain.h"
#include "ModelRenderer.h"


// Scene에 배치 되어있는 object들
void Scene::Start()
{
	unordered_set<shared_ptr<GameObject>> objects = _objects;

	for (shared_ptr<GameObject> object : objects)
	{
		object->Start();
	}
}

void Scene::Update()
{
	unordered_set<shared_ptr<GameObject>> objects = _objects;

	for (shared_ptr<GameObject> object : objects)
	{
		object->Update();
	}

	// INSTANCING
	vector<shared_ptr<GameObject>> temp;
	temp.insert(temp.end(), objects.begin(), objects.end());
	INSTANCING->Render(temp);
}

void Scene::LateUpdate()
{
	unordered_set<shared_ptr<GameObject>> objects = _objects;

	for (shared_ptr<GameObject> object : objects)
	{
		object->LateUpdate();
	}
}

void Scene::GUIRender()
{
	unordered_set<shared_ptr<GameObject>> objects = _objects;

	for (shared_ptr<GameObject> object : objects)
	{
		if (object && object->_enableGUI == true)
		{
			object->GUIRender();
		}
	}
}

void Scene::Add(shared_ptr<GameObject> object)
{
	_objects.insert(object);
	if (object->GetCamera() != nullptr)
	{
		_cameras.insert(object);
	}

	if (object->GetLight() != nullptr)
	{
		_lights.insert(object);
	}
}

void Scene::Remove(shared_ptr<GameObject> object)
{
	_objects.erase(object);
	_cameras.erase(object);
	_lights.erase(object);
}

#pragma region 수정전

//
//void ConvertScreenToWorld(int screenX, int screenY, shared_ptr<Camera> camera, Vec3& outRayOrigin, Vec3& outRayDirection)
//{
//	float viewportWidth = GRAPHICS->GetViewport().GetWidth();
//	float viewportHeight = GRAPHICS->GetViewport().GetHeight();
//
//	// 🚀 [1] Viewport → Normalized Device Coordinates (NDC) 변환
//	float viewX = (2.0f * screenX / viewportWidth) - 1.0f;
//	float viewY = 1.0f - (2.0f * screenY / viewportHeight);  // 좌표계를 뒤집음
//
//	// 🚀 [2] Projection Matrix 역변환을 사용해 View Space 좌표 얻기
//	Matrix projInv = camera->GetProjectionMatrix().Invert();
//	Vec4 rayStartNDC(viewX, viewY, 0.0f, 1.0f);
//	Vec4 rayEndNDC(viewX, viewY, 1.0f, 1.0f);
//
//	Vec4 rayStartView = XMVector4Transform(rayStartNDC, projInv);
//	Vec4 rayEndView = XMVector4Transform(rayEndNDC, projInv);
//
//	rayStartView /= rayStartView.w;
//	rayEndView /= rayEndView.w;
//
//	// 🚀 [3] View Space 기준으로 `Ray Origin`을 (0,0,0)에서 변환한 값으로 설정
//	Matrix viewInv = camera->GetViewMatrix().Invert();
//	Vec4 rayOriginWorld = XMVector4Transform(Vec4(0, 0, 0, 1), viewInv);
//	Vec4 rayEndWorld = XMVector4Transform(rayEndView, viewInv);
//
//	// 🚀 [4] Ray Origin을 `View Space`에서 변환한 값으로 설정
//	outRayOrigin = Vec3(rayOriginWorld.x, rayOriginWorld.y, rayOriginWorld.z);
//
//	// 🚀 [5] `Ray Direction`을 계산하고 강제 정규화
//	Vec3 direction = Vec3(rayEndWorld.x, rayEndWorld.y, rayEndWorld.z) - outRayOrigin;
//
//	// 🚨 Ray Direction이 (0,0,0)이면 기본값 설정
//	if (direction.Length() < 0.0001f)
//	{
//		DEBUG_LOG("Warning: Ray Direction is zero! Setting default forward direction.");
//		direction = Vec3(0, 0, 1); // 기본 방향 설정 (카메라 전방)
//	}
//
//	// 🚀 DirectX 내장 함수로 강제 정규화
//	XMVECTOR dxDirection = XMVectorSet(direction.x, direction.y, direction.z, 0.0f);
//	dxDirection = XMVector3Normalize(dxDirection);
//	XMStoreFloat3(&direction, dxDirection);
//
//	outRayDirection = direction;
//
//	// 🚨 정규화된 벡터가 단위 벡터인지 추가 검사
//	float len = outRayDirection.Length();
//	if (fabs(len - 1.0f) > 0.0001f)
//	{
//		DEBUG_LOG("Error: Ray Direction is not normalized! Length: " << len);
//		outRayDirection /= len; // 🚀 강제 보정
//	}
//
//	// 🚀 [6] 디버깅 로그 추가
//	DEBUG_LOG("ConvertScreenToWorld - Ray Origin: " << outRayOrigin.x << ", "
//		<< outRayOrigin.y << ", "
//		<< outRayOrigin.z);
//	DEBUG_LOG("ConvertScreenToWorld - Ray Direction: " << outRayDirection.x << ", "
//		<< outRayDirection.y << ", "
//		<< outRayDirection.z);
//}
//
//
//
//
//
//
//shared_ptr<class GameObject> Scene::Pick(int32 screenX, int32 screenY)
//{
//	shared_ptr<Camera> camera = GetCamera()->GetCamera();
//
//	// 🚀 ConvertScreenToWorld()를 사용하여 Ray 생성
//	Vec3 worldRayOrigin, worldRayDirection;
//	ConvertScreenToWorld(screenX, screenY, camera, worldRayOrigin, worldRayDirection);
//
//	// 🚀 Ray의 길이를 설정
//	float maxRayDistance = 1000.0f;
//	Ray ray(worldRayOrigin, worldRayDirection * maxRayDistance);
//
//	const auto& gameObjects = GetObjects();
//	float minDistance = FLT_MAX;
//	shared_ptr<GameObject> picked;
//
//	for (auto& gameObject : gameObjects)
//	{
//		if (gameObject->GetCollider() == nullptr)
//			continue;
//
//		// 🚀 [1] 충돌 검사
//		float distance = 0.f;
//		if (!gameObject->GetCollider()->Intersects(ray, OUT distance))
//			continue;
//
//		// 🚀 [2] 가장 가까운 객체 선택
//		if (distance < minDistance)
//		{
//			minDistance = distance;
//			picked = gameObject;
//		}
//	}
//
//	// 🚀 [3] Terrain과의 충돌 검사 추가
//	for (auto& gameObject : gameObjects)
//	{
//		if (gameObject->GetTerrain() == nullptr)
//			continue;
//
//		Vec3 pickPos;
//		float distance = 0.f;
//		if (!gameObject->GetTerrain()->Pick(screenX, screenY, OUT pickPos, OUT distance))
//			continue;
//
//		if (distance < minDistance)
//		{
//			minDistance = distance;
//			picked = gameObject;
//		}
//	}
//
//	return picked;
//}
//
//
////shared_ptr<class GameObject> Scene::Pick(int32 screenX, int32 screenY , Vec3& pickPos)
////{
////	shared_ptr<Camera> camera = GetCamera()->GetCamera();
////
////	float width = GRAPHICS->GetViewport().GetWidth();
////	float height = GRAPHICS->GetViewport().GetHeight();
////	//float width = static_cast<float>(GAME->GetGameDesc().width);
////	//float height = static_cast<float>(GAME->GetGameDesc().height);
////
////	Matrix projectionMatrix = camera->GetProjectionMatrix();
////
////	float viewX = (+2.0f * screenX / width - 1.0f) / projectionMatrix(0, 0);
////	float viewY = (-2.0f * screenY / height + 1.0f) / projectionMatrix(1, 1);
////
////	Matrix viewMatrix = camera->GetViewMatrix();
////	Matrix viewMatrixInv = viewMatrix.Invert();
////
////	const auto& gameObjects = GetObjects();
////
////	float minDistance = FLT_MAX;
////	shared_ptr<GameObject> picked;
////
////	for (auto& gameObject : gameObjects)
////	{
////		if (gameObject->GetCollider() == nullptr)
////			continue;
////
////		// ViewSpace에서의 Ray 정의
////		Vec4 rayOrigin = Vec4(0.0f, 0.0f, 0.0f, 1.0f);
////		Vec4 rayDir = Vec4(viewX, viewY, 1.0f, 0.0f);
////
////		// WorldSpace에서의 Ray 정의
////		Vec3 worldRayOrigin = XMVector3TransformCoord(rayOrigin, viewMatrixInv);
////		Vec3 worldRayDir = XMVector3TransformNormal(rayDir, viewMatrixInv);
////		
////		DEBUG_LOG("Ray Direction Before Normalize: " << worldRayDir.x << ", " << worldRayDir.y << ", " << worldRayDir.z);
////		worldRayDir.Normalize();
////		DEBUG_LOG("Ray Direction After Normalize: " << worldRayDir.x << ", " << worldRayDir.y << ", " << worldRayDir.z);
////
////		// WorldSpace에서 연산
////		Ray ray = Ray(worldRayOrigin, worldRayDir);
////
////		float distance = 0.f;
////		if (gameObject->GetCollider()->Intersects(ray, OUT distance) == false)
////			continue;
////
////		if (distance < minDistance)
////		{
////			minDistance = distance;
////			picked = gameObject;
////		}
////	}
////
////	for (auto& gameObject : gameObjects)
////	{
////		if (gameObject->GetTerrain() == nullptr)
////			continue;
////
////		
////		float distance = 0.f;
////		if (gameObject->GetTerrain()->Pick(screenX, screenY, OUT pickPos, OUT distance) == false)
////			continue;
////
////		DEBUG_LOG("Current Pick Pos : " << pickPos.x << " " << pickPos.z << " ");
////		if (distance < minDistance)
////		{
////			minDistance = distance;
////			picked = gameObject;
////
////		}
////	}
////
////	return picked;
////}
//shared_ptr<class GameObject> Scene::Pick(int32 screenX, int32 screenY , Vec3& pickPos)
//{
//	shared_ptr<Camera> camera = GetCamera()->GetCamera();
//
//	// 🚀 ConvertScreenToWorld()를 사용하여 Ray 생성
//	Vec3 worldRayOrigin, worldRayDirection;
//	ConvertScreenToWorld(screenX, screenY, camera, worldRayOrigin, worldRayDirection);
//
//	// 🚀 Ray의 길이를 설정
//	float maxRayDistance = 1000.0f;
//	Ray ray(worldRayOrigin, worldRayDirection * maxRayDistance);
//
//	const auto& gameObjects = GetObjects();
//	float minDistance = FLT_MAX;
//	shared_ptr<GameObject> picked;
//
//	for (auto& gameObject : gameObjects)
//	{
//		if (gameObject->GetCollider() == nullptr)
//			continue;
//
//		// 🚀 [1] 충돌 검사
//		float distance = 0.f;
//		if (!gameObject->GetCollider()->Intersects(ray, OUT distance))
//			continue;
//
//		// 🚀 [2] 가장 가까운 객체 선택
//		if (distance < minDistance)
//		{
//			minDistance = distance;
//			picked = gameObject;
//		}
//	}
//
//	// 🚀 [3] Terrain과의 충돌 검사 추가
//	for (auto& gameObject : gameObjects)
//	{
//		if (gameObject->GetTerrain() == nullptr)
//			continue;
//		float distance = 0.f;
//		if (!gameObject->GetTerrain()->Pick(screenX, screenY, OUT pickPos, OUT distance))
//			continue;
//
//		if (distance < minDistance)
//		{
//			minDistance = distance;
//			picked = gameObject;
//		}
//	}
//
//	return picked;
//}
//
//
//
//
//shared_ptr<class GameObject> Scene::PickMesh(int32 screenX, int32 screenY , Vec3& pickPos)
//{
//	shared_ptr<Camera> camera = GetCamera()->GetCamera();
//
//	float width = GRAPHICS->GetViewport().GetWidth();
//	float height = GRAPHICS->GetViewport().GetHeight();
//
//	Matrix projectionMatrix = camera->GetProjectionMatrix();
//
//	float viewX = (+2.0f * screenX / width - 1.0f) / projectionMatrix(0, 0);
//	float viewY = (-2.0f * screenY / height + 1.0f) / projectionMatrix(1, 1);
//
//	Matrix viewMatrix = camera->GetViewMatrix();
//	Matrix viewMatrixInv = viewMatrix.Invert();
//
//	const auto& gameObjects = GetObjects();
//
//	float minDistance = FLT_MAX;
//	shared_ptr<GameObject> picked;
//
//	for (auto& gameObject : gameObjects)
//	{
//		if (gameObject->GetCollider() == nullptr)
//			continue;
//
//		// ViewSpace에서의 Ray 정의
//		Vec4 rayOrigin = Vec4(0.0f, 0.0f, 0.0f, 1.0f);
//		Vec4 rayDir = Vec4(viewX, viewY, 1.0f, 0.0f);
//
//		// WorldSpace에서의 Ray 정의
//		Vec3 worldRayOrigin = XMVector3TransformCoord(rayOrigin, viewMatrixInv);
//		Vec3 worldRayDir = XMVector3TransformNormal(rayDir, viewMatrixInv);
//		worldRayDir.Normalize();
//
//		// WorldSpace에서 연산
//		Ray ray = Ray(worldRayOrigin, worldRayDir);
//
//		float distance = 0.f;
//		if (gameObject->GetCollider()->Intersects(ray, OUT distance) == false)
//			continue;
//
//		if (distance < minDistance)
//		{
//			minDistance = distance;
//			picked = gameObject;
//		}
//	}
//
//	for (auto& gameObject : gameObjects)
//	{
//		if (gameObject->GetModelRenderer() == nullptr)
//			continue;
//
//		
//		float distance = 0.f;
//		if (gameObject->GetModelRenderer()->PickMesh(screenX, screenY, OUT pickPos, OUT distance) == false)
//			continue;
//
//		if (distance < minDistance)
//		{
//			minDistance = distance;
//			picked = gameObject;
//		}
//	}
//
//	return picked;
//}
//


#pragma endregion




std::shared_ptr<class GameObject> Scene::Pick(int32 screenX, int32 screenY)
{
	shared_ptr<Camera> camera = GetCamera()->GetCamera();

	float width = GRAPHICS->GetViewport().GetWidth();
	float height = GRAPHICS->GetViewport().GetHeight();
	//float width = static_cast<float>(GAME->GetGameDesc().width);
	//float height = static_cast<float>(GAME->GetGameDesc().height);

	Matrix projectionMatrix = camera->GetProjectionMatrix();

	float viewX = (+2.0f * screenX / width - 1.0f) / projectionMatrix(0, 0);
	float viewY = (-2.0f * screenY / height + 1.0f) / projectionMatrix(1, 1);

	Matrix viewMatrix = camera->GetViewMatrix();
	Matrix viewMatrixInv = viewMatrix.Invert();

	const auto& gameObjects = GetObjects();

	float minDistance = FLT_MAX;
	shared_ptr<GameObject> picked;

	for (auto& gameObject : gameObjects)
	{
		if (gameObject->GetCollider() == nullptr)
			continue;

		// ViewSpace에서의 Ray 정의
		Vec4 rayOrigin = Vec4(0.0f, 0.0f, 0.0f, 1.0f);
		Vec4 rayDir = Vec4(viewX, viewY, 1.0f, 0.0f);

		// WorldSpace에서의 Ray 정의
		Vec3 worldRayOrigin = XMVector3TransformCoord(rayOrigin, viewMatrixInv);
		Vec3 worldRayDir = XMVector3TransformNormal(rayDir, viewMatrixInv);
		worldRayDir.Normalize();

		// WorldSpace에서 연산
		Ray ray = Ray(worldRayOrigin, worldRayDir);

		float distance = 0.f;
		if (gameObject->GetCollider()->Intersects(ray, OUT distance) == false)
			continue;

		if (distance < minDistance)
		{
			minDistance = distance;
			picked = gameObject;
		}
	}

	for (auto& gameObject : gameObjects)
	{
		if (gameObject->GetTerrain() == nullptr)
			continue;

		Vec3 pickPos;
		float distance = 0.f;
		if (gameObject->GetTerrain()->Pick(screenX, screenY, OUT pickPos, OUT distance) == false)
			continue;

		if (distance < minDistance)
		{
			minDistance = distance;
			picked = gameObject;
		}
	}

	return picked;
}


std::shared_ptr<class GameObject> Scene::Pick(int32 screenX, int32 screenY , Vec3& pickPos)
{
	shared_ptr<Camera> camera = GetCamera()->GetCamera();

	float width = GRAPHICS->GetViewport().GetWidth();
	float height = GRAPHICS->GetViewport().GetHeight();
	//float width = static_cast<float>(GAME->GetGameDesc().width);
	//float height = static_cast<float>(GAME->GetGameDesc().height);

	Matrix projectionMatrix = camera->GetProjectionMatrix();

	float viewX = (+2.0f * screenX / width - 1.0f) / projectionMatrix(0, 0);
	float viewY = (-2.0f * screenY / height + 1.0f) / projectionMatrix(1, 1);

	Matrix viewMatrix = camera->GetViewMatrix();
	Matrix viewMatrixInv = viewMatrix.Invert();

	const auto& gameObjects = GetObjects();

	float minDistance = FLT_MAX;
	shared_ptr<GameObject> picked;

	for (auto& gameObject : gameObjects)
	{
		if (gameObject->GetCollider() == nullptr)
			continue;

		// ViewSpace에서의 Ray 정의
		Vec4 rayOrigin = Vec4(0.0f, 0.0f, 0.0f, 1.0f);
		Vec4 rayDir = Vec4(viewX, viewY, 1.0f, 0.0f);

		// WorldSpace에서의 Ray 정의
		Vec3 worldRayOrigin = XMVector3TransformCoord(rayOrigin, viewMatrixInv);
		Vec3 worldRayDir = XMVector3TransformNormal(rayDir, viewMatrixInv);
		worldRayDir.Normalize();

		// WorldSpace에서 연산
		Ray ray = Ray(worldRayOrigin, worldRayDir);

		float distance = 0.f;
		if (gameObject->GetCollider()->Intersects(ray, OUT distance) == false)
			continue;

		if (distance < minDistance)
		{
			minDistance = distance;
			picked = gameObject;
		}
	}

	for (auto& gameObject : gameObjects)
	{
		if (gameObject->GetTerrain() == nullptr)
			continue;
		float distance = 0.f;
		if (gameObject->GetTerrain()->Pick(screenX, screenY, OUT pickPos, OUT distance) == false)
			continue;

		if (distance < minDistance)
		{
			minDistance = distance;
			picked = gameObject;
		}
	}

	return picked;
}

