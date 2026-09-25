#include "pch.h"
#include "InstancingManager.h"
#include "InstancingBuffer.h"
#include "GameObject.h"
#include "MeshRenderer.h"
#include "ModelRenderer.h"
#include "ModelAnimator.h"
#include "ParticleRenderer.h"
#include "Transform.h"
#include "Camera.h"

void InstancingManager::Render(vector<shared_ptr<GameObject>>& gameObjects)
{
	ClearData();

	RenderMeshRenderer(gameObjects, false);
	RenderModelRenderer(gameObjects);
	RenderAnimRenderer(gameObjects);
	RenderParticleRenderer(gameObjects);
	RenderMeshRenderer(gameObjects, true);

	// Passes 14-16 use overlay-only blend/depth/rasterizer state. Effects11 does not
	// restore the previous pipeline state after a pass, so reset it before the
	// next camera/frame renders regular world geometry.
	DC->OMSetBlendState(nullptr, nullptr, 0xffffffff);
	DC->OMSetDepthStencilState(nullptr, 0);
	DC->RSSetState(nullptr);


}

void InstancingManager::RenderMeshRenderer(vector<shared_ptr<GameObject>>& gameObjects, bool overlayOnly)
{
	map<pair<int32, InstanceID>, vector<shared_ptr<GameObject>>> cache;

	for (shared_ptr<GameObject>& gameObject : gameObjects)
	{
		auto renderer = gameObject->GetMeshRenderer();
		if (renderer == nullptr)
			continue;
		const bool isWorldOverlay = renderer->IsWorldOverlay();
		if (isWorldOverlay != overlayOnly)
			continue;

		const InstanceID instanceId = renderer->GetInstanceID();
		cache[{ renderer->GetOverlayOrder(), instanceId }].push_back(gameObject);
	}

	for (auto& pair : cache)
	{
		const vector<shared_ptr<GameObject>>& vec = pair.second;
		const InstanceID instanceId = pair.first.second;

		for (int32 i = 0; i < vec.size(); i++)
		{
			const shared_ptr<GameObject>& gameObject = vec[i];
			InstancingData data;
			data.world = gameObject->GetTransform()->GetWorldMatrix();
			AddData(instanceId, data);
		}

		shared_ptr<InstancingBuffer>& buffer = _buffers[instanceId];
		vec[0]->GetMeshRenderer()->RenderInstancing(buffer);
	}
}
void InstancingManager::RenderModelRenderer(vector<shared_ptr<GameObject>>& gameObjects)
{
	map<InstanceID, vector<shared_ptr<GameObject>>> cache;

	for (shared_ptr<GameObject>& gameObject : gameObjects)
	{
		if (gameObject->GetModelRenderer() == nullptr)
			continue;

		const InstanceID instanceId = gameObject->GetModelRenderer()->GetInstanceID();
		cache[instanceId].push_back(gameObject);
	}

	for (auto& pair : cache)
	{
		const vector<shared_ptr<GameObject>>& vec = pair.second;

		//if (vec.size() == 1)
		//{
		//	vec[0]->GetMeshRenderer()->RenderSingle();
		//}
		//else
		{
			const InstanceID instanceId = pair.first;

			for (int32 i = 0; i < vec.size(); i++)
			{
				const shared_ptr<GameObject>& gameObject = vec[i];
				InstancingData data;
				data.world = gameObject->GetTransform()->GetWorldMatrix();
				AddData(instanceId, data);
			}

			shared_ptr<InstancingBuffer>& buffer = _buffers[instanceId];
			vec[0]->GetModelRenderer()->RenderInstancing(buffer);
		}
	}
}

void InstancingManager::RenderAnimRenderer(vector<shared_ptr<GameObject>>& gameObjects)
{
	map<InstanceID, vector<shared_ptr<GameObject>>> cache;

	for (shared_ptr<GameObject>& gameObject : gameObjects)
	{
		if (gameObject->GetModelAnimator() == nullptr)
			continue;

		const InstanceID instanceId = gameObject->GetModelAnimator()->GetInstanceID();
		cache[instanceId].push_back(gameObject);
	}

	for (auto& pair : cache)
	{
		shared_ptr<InstancedTweenDesc> tweenDesc = make_shared<InstancedTweenDesc>();

		const vector<shared_ptr<GameObject>>& vec = pair.second;

		//if (vec.size() == 1)
		//{
		//	vec[0]->GetModelAnimator()->RenderSingle();
		//}
		//else
		{
			const InstanceID instanceId = pair.first;

			for (int32 i = 0; i < vec.size(); i++)
			{
				const shared_ptr<GameObject>& gameObject = vec[i];
				InstancingData data;
				data.world = gameObject->GetTransform()->GetWorldMatrix();

				AddData(instanceId, data);

				// INSTANCING
				gameObject->GetModelAnimator()->UpdateTweenData();
				tweenDesc->tweens[i] = gameObject->GetModelAnimator()->GetTweenDesc();
			}

			vec[0]->GetModelAnimator()->GetShader()->PushTweenData(*tweenDesc.get());

			shared_ptr<InstancingBuffer>& buffer = _buffers[instanceId];
			vec[0]->GetModelAnimator()->RenderInstancing(buffer);
		}
	}
}

void InstancingManager::RenderParticleRenderer(vector<shared_ptr<GameObject>>& gameObjects)
{
	//map<InstanceID, vector<shared_ptr<GameObject>>> cache;

	//for (shared_ptr<GameObject>& gameObject : gameObjects)
	//{
	//	if (gameObject->GetParticleRenderer() == nullptr)
	//		continue;

	//	const InstanceID instanceId = gameObject->GetParticleRenderer()->GetInstanceID();
	//	cache[instanceId].push_back(gameObject);
	//}

	//for (auto& pair : cache)
	//{
	//	const vector<shared_ptr<GameObject>>& vec = pair.second;
	//	const InstanceID instanceId = pair.first;

	//	for (int32 i = 0; i < vec.size(); i++)
	//	{
	//		const shared_ptr<GameObject>& gameObject = vec[i];
	//		auto renderer = gameObject->GetParticleRenderer();

	//		renderer->UpdateParticles(DT);
	//		renderer->UpdateColor();

	//		for (uint32 j = 0; j < renderer->_drawCount; ++j)
	//		{
	//			InstancingData data;
	//			data.world = renderer->_instances[j].world; // XMMatrix
	//			AddData(instanceId, data);
	//		}
	//	}

	//	shared_ptr<InstancingBuffer>& buffer = _buffers[instanceId];
	//	vec[0]->GetParticleRenderer()->RenderInstancing(buffer);
	//}
}


void InstancingManager::AddData(InstanceID instanceId, InstancingData& data)
{
	if (_buffers.find(instanceId) == _buffers.end())
		_buffers[instanceId] = make_shared<InstancingBuffer>();

	_buffers[instanceId]->AddData(data);
}


void InstancingManager::ClearData()
{
	for (auto& pair : _buffers)
	{
		shared_ptr<InstancingBuffer>& buffer = pair.second;
		buffer->ClearData();
	}
}
