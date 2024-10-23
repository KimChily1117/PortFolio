#include "pch.h"
#include "Player.h"
#include "Texture.h"
#include "ResourceManager.h"
#include "TimeManager.h"
#include "InputManager.h"
#include "CameraComponent.h"

Player::Player()
{
	{
		_flipbookUp = GET_SINGLE(ResourceManager)->GetFlipbook(L"FB_MoveUp");
		_flipbookDown = GET_SINGLE(ResourceManager)->GetFlipbook(L"FB_MoveDown");
		_flipbookLeft = GET_SINGLE(ResourceManager)->GetFlipbook(L"FB_MoveLeft");
		_flipbookRight = GET_SINGLE(ResourceManager)->GetFlipbook(L"FB_MoveRight");
		
		CameraComponent* camera = new CameraComponent();

		AddComponent(camera);
	}
}

Player::~Player()
{

}

void Player::BeginPlay()
{
	Super::BeginPlay();

	SetFlipbook(_flipbookRight);
}

void Player::Tick()
{
	Super::Tick();

	float deltaTime = GET_SINGLE(TimeManager)->GetDeltaTime();

	// TODO
	if (GET_SINGLE(InputManager)->GetButton(KeyType::W))
	{
		_pos.y -= 200 * deltaTime;
		SetFlipbook(_flipbookUp);
	}
	else if (GET_SINGLE(InputManager)->GetButton(KeyType::S))
	{
		_pos.y += 200 * deltaTime;
		SetFlipbook(_flipbookDown);
	}
	else  if (GET_SINGLE(InputManager)->GetButton(KeyType::A))
	{
		_pos.x -= 200 * deltaTime;
		SetFlipbook(_flipbookLeft);
	}
	else  if (GET_SINGLE(InputManager)->GetButton(KeyType::D))
	{
		_pos.x += 200 * deltaTime;
		SetFlipbook(_flipbookRight);
	}

	else if (GET_SINGLE(InputManager)->GetButton(KeyType::SpaceBar))
	{
		Jump();
	}

	TickGravity();

}


void Player::Render(HDC hdc)
{
	Super::Render(hdc);
}

void Player::OnComponentBeginOverlap(Collider* collider, Collider* other)
{
	Super::OnComponentBeginOverlap(collider, other);
	
	_isGround = true;
	_isJumping = false;
}

void Player::Jump()
{
	float deltaTime = GET_SINGLE(TimeManager)->GetDeltaTime();

	if (_isJumping)
		return;

	_isJumping = true;
	_isGround = false;

	_speed.y = -500;


}

void Player::TickGravity()
{
	float deltaTime = GET_SINGLE(TimeManager)->GetDeltaTime();
	if (deltaTime > 0.1f)
		return;

	if (_isGround)
		return;
	_speed.y += _gravity * deltaTime;
	_pos.y += _speed.y * deltaTime;

}
