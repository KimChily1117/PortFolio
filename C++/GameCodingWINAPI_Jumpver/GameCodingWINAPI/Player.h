#pragma once
#include "FlipbookActor.h"

class Flipbook;

class Player : public FlipbookActor
{
	using Super = FlipbookActor;


public:
	Player();
	virtual ~Player() override;

	virtual void BeginPlay() override;
	virtual void Tick() override;
	virtual void Render(HDC hdc) override;		

	virtual void OnComponentBeginOverlap(Collider* collider, Collider* other) override;

	// Å×½ºÆ®
	void Jump();
	void TickGravity();

private:
	Flipbook* _flipbookUp = nullptr;
	Flipbook* _flipbookDown = nullptr;
	Flipbook* _flipbookLeft = nullptr;
	Flipbook* _flipbookRight = nullptr;

	bool _isJumping;
	bool _isGround;

	Vec2 _speed = {};
	int32 _gravity = 500;
};

