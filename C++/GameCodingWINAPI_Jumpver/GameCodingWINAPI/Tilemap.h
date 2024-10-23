#pragma once
#include "ResourceBase.h"


struct Tile
{
	// TODO
	int32 value = 0;
};



class Tilemap : public ResourceBase
{
public:
	Tilemap();
	virtual ~Tilemap();
public:
	void SaveFile(const wstring& path) override;
	void LoadFile(const wstring& path) override;

	Vec2Int GetMapSize() { return _mapSize; }
	int32 GetTileSize() { return _tileSize; }
	Tile* GetTileAt(Vec2Int pos);
	vector<vector<Tile>>& GetTiles() { return _tiles; }

	void SetMapSize(Vec2Int size);
	void SetTileSize(int32 size);

protected:
	Vec2Int _mapSize = {};
	int32 _tileSize = 0;
	vector<vector<Tile>> _tiles;
};

