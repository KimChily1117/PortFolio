#pragma once
#include "Component.h"
#include "Tilemap.h"

class Terrain : public Component
{
	using Super = Component;
public:
	Terrain();
	~Terrain();

	void Create(int32 sizeX, int32 sizeZ, shared_ptr<Material> material);
	bool Pick(int32 screenX, int32 screenY, Vec3& pickPos, float& distance);

	int32 GetSizeX() { return _sizeX; }
	int32 GetSizeZ() { return _sizeZ; }

	void SetTilemap(shared_ptr<Tilemap> tilemap) { _tilemap = tilemap; }
	shared_ptr<Tilemap> GetTilemap() { return _tilemap; }

	Vec3 GetTileCorrectedPosition(Vec3 pickPos);
	bool IsPointInTriangle(const Vec3& P, const Vec3& A, const Vec3& B, const Vec3& C, Vec3& outBarycentric);
	Vec3 BarycentricInterpolation(const Vec3& A, const Vec3& B, const Vec3& C, const Vec3& barycentric);


private:
	shared_ptr<Mesh> _mesh;
	int32 _sizeX = 0;
	int32 _sizeZ = 0;

	shared_ptr<class Tilemap> _tilemap;
};

