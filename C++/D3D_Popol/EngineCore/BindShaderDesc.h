#pragma once
#include "ConstantBuffer.h"

class Shader;

struct GlobalDesc
{
	Matrix V = Matrix::Identity;
	Matrix P = Matrix::Identity;
	Matrix VP = Matrix::Identity;
	Matrix VInv = Matrix::Identity;
};

struct TransformDesc
{
	Matrix W = Matrix::Identity;
};

// Light
struct LightDesc
{
	Color ambient = Color(1.f, 1.f, 1.f, 1.f);
	Color diffuse = Color(1.f, 1.f, 1.f, 1.f);
	Color specular = Color(1.f, 1.f, 1.f, 1.f);
	Color emissive = Color(1.f, 1.f, 1.f, 1.f);

	Vec3 direction;
	float padding0;
};

struct MaterialDesc
{
	Color ambient = Color(0.f, 0.f, 0.f, 1.f);
	Color diffuse = Color(1.f, 1.f, 1.f, 1.f);
	Color specular = Color(0.f, 0.f, 0.f, 1.f);
	Color emissive = Color(0.f, 0.f, 0.f, 1.f);
};

// Bone
#define MAX_MODEL_TRANSFORMS 700
#define MAX_MODEL_KEYFRAMES 500
#define MAX_MODEL_INSTANCE 500

struct BoneDesc
{
	Matrix transforms[MAX_MODEL_TRANSFORMS];
};

struct KeyframeDesc
{
	int32 animIndex = 0;
	uint32 currFrame = 0;
	uint32 nextFrame = 0;
	float ratio = 0.f;
	float sumTime = 0.f;
	float speed = 1.f;
	uint32 loop = 1;  // ✅ bool 대신 uint32 사용 (GPU 친화적)
	Vec3 padding;      // ✅ 16-byte 정렬을 위해 Vec3 추가
};


using AnimationEvent = function<void()>;


struct TweenDesc
{
	TweenDesc()
	{
		curr.animIndex = 0;
		next.animIndex = -1;
	}

	void ClearNextAnim()
	{
		next.animIndex = -1;
		next.currFrame = 0;
		next.nextFrame = 0;
		next.sumTime = 0;
		tweenSumTime = 0;
		tweenRatio = 0;
	}

	// ✅ 애니메이션 특정 시점에 실행할 이벤트 추가
	void AddAnimationEvent(float timeRatio, std::function<void()> callback)
	{
		animationEvents[timeRatio] = callback;
	}

	// ✅ 애니메이션 이벤트 실행
	void ExecuteAnimationEvents(float ratio)
	{
		auto it = animationEvents.lower_bound(ratio);
		while (it != animationEvents.end() && it->first <= ratio)
		{
			it->second();  // 등록된 이벤트 실행
			it = animationEvents.erase(it);  // 실행 후 제거
		}
	}

	float tweenDuration = 1.0f;
	float tweenRatio = 0.f;
	float tweenSumTime = 0.f;
	float padding = 0.f;
	KeyframeDesc curr;
	KeyframeDesc next;

	std::map<float, std::function<void()>> animationEvents; // ✅ 애니메이션 이벤트 저장소
};


struct InstancedTweenDesc
{
	TweenDesc tweens[MAX_MODEL_INSTANCE];
};


struct UIFillMountDesc
{
	float ratio = 0.f; // 0 ~ 1 사이의 값을 보간해서 사용
	float padding[3];  // 12 bytes 추가 (4 + 12 = 16 bytes)
};
