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




struct ParticleData // 파티클 데이터 (파티클 옵션들)
{
	// 토글 옵션
	bool isLoop = true;         // 반복 재생인가?
	bool isAdditive = true;     // 화소 색채가 누적(반투명)인가?
	bool isBillboard = true;    // 항상 정면을 향하게 할 것인가?

	// 규모 옵션
	UINT count = 100;           // 입자 개수
	float duration = 1.0f;      // 재생 시간

	// 파티클 제어 옵션 (min : 최소, max : 최대)
	Vec3 minVelocity = { -1, -1, -1 };   // 허용 속도 (방향, 속력)
	Vec3 maxVelocity = { +1, +1, +1 };
	Vec3 minAccelation;                  // 가속 (움직이는 중 변동값)
	Vec3 maxAccelation;
	Vec3 minStartScale = { 1, 1, 1 };    // 크기 (입자 생성시)
	Vec3 maxStartScale = { 1, 1, 1 };
	Vec3 minEndScale = { 1, 1, 1 };      // 크기 (입자 소멸시)
	Vec3 maxEndScale = { 1, 1, 1 };
	float minSpeed = 1.0f;                  // 속력 (입자 이동)
	float maxSpeed = 3.0f;
	float minAngularVelocity = -10.0f;      // 방향 편향
	float maxAngularVelocity = +10.0f;
	float minStartTime = 0.0f;              // 입자별 지연시간
	float maxStartTime = 0.0f;
	XMFLOAT4 startColor = { 1, 1, 1, 1 };     // 입자의 색깔
	XMFLOAT4 endColor = { 1, 1, 1, 1 };
};

struct ParticleDesc
{
	Color startColor;
	Color endColor;

	Vec3 velocity;
	float drawDistance;

	Vec3 extent;
	float turbulence;

	Vec3 origin;
	float elapsedTime;
};
