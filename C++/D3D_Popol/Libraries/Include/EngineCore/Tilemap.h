#pragma once
#include "ResourceBase.h"

struct Tile
{
	Vec3 position;
	float height;
	bool isWalkable;
	int32 value = 0;
};


class Tilemap : public ResourceBase
{
	using Super = ResourceBase;
public:
	Tilemap();
	virtual ~Tilemap() override;

	virtual void Load(const wstring& path) override;
	virtual void Save(const wstring& path) override;

	Vec3 GetMapSize() { return _mapSize; }
	int32 GetTileSize() { return _tileSize; }
	Tile* GetTileAt(Vec3 pos);
	vector<vector<Tile>>& GetTiles() { return _tiles; };

	void SetMapSize(Vec3 size);
	void SetTileSize(int32 size);

	Vec3 ConvertWorldToTile(Vec3 worldPos);

	// 추가 기능
	bool PickTile(Vec3 worldPos, Vec3& outTilePos); // 🔥 Picking (Ray → Tile 변환)
	bool CheckCollision(Vec3 position); // 🔥 충돌 검사 (벽, 물 등)
	void ApplySkillRange(Vec3 skillPos, float skillRange); // 🔥 스킬 범위 판정
	void UpdateFogOfWar(Vec3 playerPos, float visionRange); // 🔥 Fog of War 시스템

private:
	Vec3 _mapSize = {};
	int32 _tileSize = {};
	vector<vector<Tile>> _tiles;
};

