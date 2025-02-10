#include "pch.h"
#include "ModelRenderer.h"
#include "Material.h"
#include "ModelMesh.h"
#include "Model.h"
#include "InstancingBuffer.h"
#include "Camera.h"
#include "Light.h"

ModelRenderer::ModelRenderer(shared_ptr<Shader> shader)
	: Super(ComponentType::ModelRenderer), _shader(shader)
{

}

ModelRenderer::~ModelRenderer()
{

}




void ModelRenderer::SetModel(shared_ptr<Model> model)
{
	_model = model;

	const auto& materials = _model->GetMaterials();
	for (auto& material : materials)
	{
		material->SetShader(_shader);
	}
}

bool ModelRenderer::PickMesh(int32 screenX, int32 screenY, Vec3& pickPos, float& distance)
{
	if (!_model)
		return false;

	// Step 1: Ray ���� (��ũ�� ��ǥ -> ���� ���� ��ȯ)
	Matrix W = Matrix::Identity; // ���� ���� ��ȯ ��� (�ʿ��ϸ� `_model->GetWorldMatrix()`�� ��ü)
	Matrix V = Camera::S_MatView;
	Matrix P = Camera::S_MatProjection;

	Viewport& vp = GRAPHICS->GetViewport();

	// Unproject�� �̿��� ��ũ�� ��ǥ�� ���� �������� ��ȯ
	Vec3 start = vp.Unproject(Vec3(screenX, screenY, 0), W, V, P);
	Vec3 end = vp.Unproject(Vec3(screenX, screenY, 1), W, V, P);

	// Ray ����
	Vec3 rayDir = end - start;
	rayDir.Normalize();
	Ray ray(start, rayDir);

	// Step 2: ���� �޽� ��������
	const auto& meshes = _model->GetMeshes();
	float minDistance = FLT_MAX;
	bool hit = false;

	// Step 3: ��� �ﰢ�� �˻�
	for (const auto& mesh : meshes)
	{
		const auto& vertices = mesh->geometry->GetVertices();
		const auto& indices = mesh->geometry->GetIndices();

		for (size_t i = 0; i < indices.size(); i += 3)
		{
			Vec3 v0 = vertices[indices[i + 0]].position;
			Vec3 v1 = vertices[indices[i + 1]].position;
			Vec3 v2 = vertices[indices[i + 2]].position;

			float dist;
			if (ray.Intersects(v0, v1, v2, OUT dist))
			{
				if (dist < minDistance) // ���� ����� �浹���� ����
				{
					minDistance = dist;
					hit = true;
					pickPos = ray.position + ray.direction * minDistance; // ��Ȯ�� ��ŷ ��ġ ����
				}
			}
		}
	}

	// ���������� ��ŷ�� �Ǿ����� Ȯ��
	if (hit)
	{
		distance = minDistance;
		return true;
	}

	return false;
}


void ModelRenderer::RenderInstancing(shared_ptr<class InstancingBuffer>& buffer)
{
	if (_model == nullptr)
		return;

	// GlobalData
	_shader->PushGlobalData(Camera::S_MatView, Camera::S_MatProjection);

	// Light
	auto lightObj = SCENE->GetCurrentScene()->GetLight();
	if (lightObj)
		_shader->PushLightData(lightObj->GetLight()->GetLightDesc());

	// Bones
	BoneDesc boneDesc;

	const uint32 boneCount = _model->GetBoneCount();
	for (uint32 i = 0; i < boneCount; i++)
	{
		shared_ptr<ModelBone> bone = _model->GetBoneByIndex(i);
		boneDesc.transforms[i] = bone->transform;
	}
	_shader->PushBoneData(boneDesc);

	const auto& meshes = _model->GetMeshes();
	for (auto& mesh : meshes)
	{
		if (mesh->material)
			mesh->material->Update();

		// BoneIndex
		_shader->GetScalar("BoneIndex")->SetInt(mesh->boneIndex);

		// IA
		mesh->vertexBuffer->PushData();
		mesh->indexBuffer->PushData();

		buffer->PushData();
		DC->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

		_shader->DrawIndexedInstanced(0, _pass, mesh->indexBuffer->GetCount(), buffer->GetCount());
	}
}

InstanceID ModelRenderer::GetInstanceID()
{
	return make_pair((uint64)_model.get(), (uint64)_shader.get());
}
