#include "pch.h"
#include "ModelAnimator.h"
#include "Material.h"
#include "ModelMesh.h"
#include "Model.h"
#include "ModelAnimation.h"
#include "InstancingBuffer.h"
#include "Camera.h"
#include "Light.h"
#include <ostream>

ModelAnimator::ModelAnimator(shared_ptr<Shader> shader)
	: Super(ComponentType::ModelAnimator), _shader(shader)
{
	//// TEST
	//_tweenDesc.curr.animIndex = 1;
	//_tweenDesc.tweenSumTime += rand() % 100;
}

ModelAnimator::~ModelAnimator()
{

}

void ModelAnimator::SetModel(shared_ptr<Model> model)
{
	_model = model;

	const auto& materials = _model->GetMaterials();
	for (auto& material : materials)
	{
		material->SetShader(_shader);
	}
}

void ModelAnimator::Update()
{
	// Not Use
}
void ModelAnimator::BlendToAnimation(int animIndex, float blendTime)
{
	if (_tweenDesc.curr.animIndex == animIndex)
		return; // 이미 같은 애니메이션이면 변경 안 함

	_tweenDesc.next.animIndex = animIndex;
	_tweenDesc.tweenDuration = blendTime;
	_tweenDesc.tweenSumTime = 0.0f;

	DEBUG_LOG("[Animation] 🔄 Blending to Animation " << animIndex << " over " << blendTime << " seconds");
}

float ModelAnimator::GetAnimationDuration(int animIndex)
{
	auto animation = _model->GetAnimationByIndex(animIndex);
	if (animation)
		return animation->frameCount / animation->frameRate; // 프레임 수 / 초당 프레임 수
	return 1.0f; // 기본값 (1초)
}

void ModelAnimator::SetAnimation(int32 animIndex, bool loop)
{
	TweenDesc& desc = _tweenDesc;
	desc.curr.animIndex = animIndex;
	desc.curr.currFrame = 0;
	desc.curr.sumTime = 0;
	desc.curr.loop = loop ? 1 : 0; // ✅ uint32로 저장 (GPU 호환)
	DEBUG_LOG("[Animation] 🎬 즉시 애니메이션 변경: " << animIndex << " (Loop: " << loop << ")");
}



void ModelAnimator::UpdateTweenData()
{
	TweenDesc& desc = _tweenDesc;
	desc.curr.sumTime += DT;

	// ✅ 현재 애니메이션 실행
	{
		shared_ptr<ModelAnimation> currentAnim = _model->GetAnimationByIndex(desc.curr.animIndex);
		if (currentAnim)
		{
			float timePerFrame = 1.0f / (currentAnim->frameRate * desc.curr.speed);
			if (desc.curr.sumTime >= timePerFrame)
			{
				desc.curr.sumTime = 0;
				int prevFrame = desc.curr.currFrame;
				desc.curr.currFrame++;

				// ✅ 애니메이션 종료 체크 (루프 여부 반영)
				if (desc.curr.currFrame >= currentAnim->frameCount)
				{
					if (desc.curr.loop) // ✅ 루프 애니메이션이면 처음으로 리셋
					{
						desc.curr.currFrame = 0;
					}
					else
					{
						desc.curr.currFrame = currentAnim->frameCount - 1; // ✅ 마지막 프레임 유지
						if (_animationEndCallback)
						{
							_animationEndCallback(); // ✅ 콜백 실행
							_animationEndCallback = nullptr; // ✅ 실행 후 초기화
						}
					}
				}

				desc.curr.nextFrame = (desc.curr.currFrame + 1) % currentAnim->frameCount;
			}

			desc.curr.ratio = (desc.curr.sumTime / timePerFrame);
		}
	}

	// ✅ 다음 애니메이션이 예약된 경우 블렌딩 처리
	if (desc.next.animIndex >= 0)
	{
		desc.tweenSumTime += DT;
		desc.tweenRatio = desc.tweenSumTime / desc.tweenDuration;

		if (desc.tweenRatio >= 1.0f)
		{
			// ✅ 블렌딩 완료 → 다음 애니메이션으로 변경
			desc.curr = desc.next;
			desc.ClearNextAnim();
		}
		else
		{
			shared_ptr<ModelAnimation> nextAnim = _model->GetAnimationByIndex(desc.next.animIndex);
			desc.next.sumTime += DT;

			float timePerFrame = 1.0f / (nextAnim->frameRate * desc.next.speed);
			if (desc.next.ratio >= 1.0f)
			{
				desc.next.sumTime = 0;
				desc.next.currFrame = (desc.next.currFrame + 1) % nextAnim->frameCount;
				desc.next.nextFrame = (desc.next.currFrame + 1) % nextAnim->frameCount;
			}

			desc.next.ratio = desc.next.sumTime / timePerFrame;
		}
	}
}


void ModelAnimator::RenderInstancing(shared_ptr<class InstancingBuffer>& buffer)
{
	if (_model == nullptr)
		return;
	if (_texture == nullptr)
		CreateTexture();

	// GlobalData
	_shader->PushGlobalData(Camera::S_MatView, Camera::S_MatProjection);

	// Light
	auto lightObj = SCENE->GetCurrentScene()->GetLight();
	if (lightObj)
		_shader->PushLightData(lightObj->GetLight()->GetLightDesc());

	// SRV를 통해 정보 전달
	_shader->GetSRV("TransformMap")->SetResource(_srv.Get());

	// Bones
	shared_ptr<BoneDesc> boneDesc = make_shared<BoneDesc>();

	//BoneDesc boneDesc;

	const uint32 boneCount = _model->GetBoneCount();
	for (uint32 i = 0; i < boneCount; i++)
	{
		shared_ptr<ModelBone> bone = _model->GetBoneByIndex(i);
		boneDesc->transforms[i] = bone->transform;

	}
	_shader->PushBoneData(*boneDesc);

	const auto& meshes = _model->GetMeshes();
	for (auto& mesh : meshes)
	{
		if (mesh->material)
			mesh->material->Update();

		// BoneIndex
		_shader->GetScalar("BoneIndex")->SetInt(mesh->boneIndex);

		_shader->GetMatrix("MeshTransformMatrix")->SetMatrix(reinterpret_cast<const float*>(&mesh->transformMatrix));

		mesh->vertexBuffer->PushData();
		mesh->indexBuffer->PushData();

		buffer->PushData();
		DC->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

		_shader->DrawIndexedInstanced(0, _pass, mesh->indexBuffer->GetCount(), buffer->GetCount());
	}
}

InstanceID ModelAnimator::GetInstanceID()
{
	return make_pair((uint64)_model.get(), (uint64)_shader.get());
}

void ModelAnimator::CreateTexture()
{
	if (_model->GetAnimationCount() == 0)
		return;

	_animTransforms.resize(_model->GetAnimationCount());
	for (uint32 i = 0; i < _model->GetAnimationCount(); i++)
		CreateAnimationTransform(i);

	// Creature Texture
	{
		D3D11_TEXTURE2D_DESC desc;
		ZeroMemory(&desc, sizeof(D3D11_TEXTURE2D_DESC));
		desc.Width = MAX_MODEL_TRANSFORMS * 4;
		desc.Height = MAX_MODEL_KEYFRAMES;
		desc.ArraySize = _model->GetAnimationCount();
		desc.Format = DXGI_FORMAT_R32G32B32A32_FLOAT; // 16바이트
		desc.Usage = D3D11_USAGE_IMMUTABLE;
		desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
		desc.MipLevels = 1;
		desc.SampleDesc.Count = 1;

		const uint32 dataSize = MAX_MODEL_TRANSFORMS * sizeof(Matrix);
		const uint32 pageSize = dataSize * MAX_MODEL_KEYFRAMES;
		void* mallocPtr = ::malloc(pageSize * _model->GetAnimationCount());

		// 파편화된 데이터를 조립한다.
		for (uint32 c = 0; c < _model->GetAnimationCount(); c++)
		{
			uint32 startOffset = c * pageSize;

			BYTE* pageStartPtr = reinterpret_cast<BYTE*>(mallocPtr) + startOffset;

			for (uint32 f = 0; f < MAX_MODEL_KEYFRAMES; f++)
			{
				void* ptr = pageStartPtr + dataSize * f;
				::memcpy(ptr, _animTransforms[c].transforms[f].data(), dataSize);
			}
		}

		// 리소스 만들기
		vector<D3D11_SUBRESOURCE_DATA> subResources(_model->GetAnimationCount());

		for (uint32 c = 0; c < _model->GetAnimationCount(); c++)
		{
			void* ptr = (BYTE*)mallocPtr + c * pageSize;
			subResources[c].pSysMem = ptr;
			subResources[c].SysMemPitch = dataSize;
			subResources[c].SysMemSlicePitch = pageSize;
		}

		HRESULT hr = DEVICE->CreateTexture2D(&desc, subResources.data(), _texture.GetAddressOf());
		CHECK(hr);

		::free(mallocPtr);
	}

	// Create SRV
	{
		D3D11_SHADER_RESOURCE_VIEW_DESC desc;
		ZeroMemory(&desc, sizeof(desc));
		desc.Format = DXGI_FORMAT_R32G32B32A32_FLOAT;
		desc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2DARRAY;
		desc.Texture2DArray.MipLevels = 1;
		desc.Texture2DArray.ArraySize = _model->GetAnimationCount();

		HRESULT hr = DEVICE->CreateShaderResourceView(_texture.Get(), &desc, _srv.GetAddressOf());
		CHECK(hr);
	}
}

void ModelAnimator::CreateAnimationTransform(uint32 index)
{
	vector<Matrix> tempAnimBoneTransforms(MAX_MODEL_TRANSFORMS, Matrix::Identity);

	shared_ptr<ModelAnimation> animation = _model->GetAnimationByIndex(index);

	for (uint32 f = 0; f < animation->frameCount; f++)
	{
		for (uint32 b = 0; b < _model->GetBoneCount(); b++)
		{
			shared_ptr<ModelBone> bone = _model->GetBoneByIndex(b);

			Matrix matAnimation;

			shared_ptr<ModelKeyframe> frame = animation->GetKeyframe(bone->name);
			if (frame != nullptr)
			{
				ModelKeyframeData& data = frame->transforms[f];
		
				Matrix S, R, T;
				S = Matrix::CreateScale(data.scale.x, data.scale.y, data.scale.z);
				R = Matrix::CreateFromQuaternion(data.rotation);
				T = Matrix::CreateTranslation(data.translation.x, data.translation.y, data.translation.z);

				matAnimation = S * R * T;
			}
			else
			{
				matAnimation = Matrix::Identity;
			}

			// [ !!!!!!! ]
			Matrix toRootMatrix = bone->transform;
			Matrix invGlobal = toRootMatrix.Invert();

			int32 parentIndex = bone->parentIndex;

			Matrix matParent = Matrix::Identity;
			if (parentIndex >= 0)
				matParent = tempAnimBoneTransforms[parentIndex];

			tempAnimBoneTransforms[b] = matAnimation * matParent;

			// 결론
			_animTransforms[index].transforms[f][b] = invGlobal * tempAnimBoneTransforms[b];
		}
	}
}
