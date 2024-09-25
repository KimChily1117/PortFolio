#include "pch.h"
#include "SpriteActor.h"
#include "Sprite.h"

SpriteActor::SpriteActor()
{

}

SpriteActor::~SpriteActor()
{

}

void SpriteActor::BeginPlay()
{	
	Super::BeginPlay();

}

void SpriteActor::Tick()
{
	Super::Tick();
}

void SpriteActor::Render(HDC hdc)
{
	Super::Render(hdc);

	if (_sprite == nullptr)
		return;
	Vec2Int size = _sprite->GetSize();
}
