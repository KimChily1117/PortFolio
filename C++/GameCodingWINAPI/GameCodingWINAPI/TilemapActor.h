#pragma once

#include "Actor.h"

class Tilemap;

class TilemapActor : public Actor
{

	using Super = Actor;

public:

	TilemapActor();
	virtual ~TilemapActor();



	void BeginPlay() override;
	void Tick() override;
	void Render(HDC hdc) override;

	void TilePicking();

	void SetTilemap(Tilemap* tilemap) { _tilemap = tilemap; }
	Tilemap* GetTilemap() { return _tilemap; }

	void SetShowDebug(bool showDebug) { _showDebug = showDebug; }

protected:
	Tilemap* _tilemap = nullptr;
	bool _showDebug = false;
};

