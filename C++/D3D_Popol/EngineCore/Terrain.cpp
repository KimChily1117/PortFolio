#include "pch.h"
#include "Terrain.h"
#include "MeshRenderer.h"
#include "Camera.h"

Terrain::Terrain() : Component(ComponentType::Terrain)
{

}

Terrain::~Terrain()
{

}

void Terrain::Create(int32 sizeX, int32 sizeZ, shared_ptr<Material> material)
{
	_sizeX = sizeX;
	_sizeZ = sizeZ;

	auto go = _gameObject.lock();

	go->GetOrAddTransform();

	if (go->GetMeshRenderer() == nullptr)
		go->AddComponent(make_shared<MeshRenderer>());

	_mesh = make_shared<Mesh>();
	_mesh->CreateGrid(sizeX, sizeZ);

	go->GetMeshRenderer()->SetMesh(_mesh);
	go->GetMeshRenderer()->SetPass(0);
	go->GetMeshRenderer()->SetMaterial(material);

	// Create Tilemap
	if (!_tilemap)
	{
		_tilemap = make_shared<Tilemap>();
		_tilemap->SetMapSize(Vec3(sizeX, 0, sizeZ));
		_tilemap->SetTileSize(1);
		_tilemap->Save(L"..\\Resources\\MapData\\TilemapData.txt");
	}
}

bool Terrain::Pick(int32 screenX, int32 screenY, Vec3& pickPos, float& distance)
{
	if (!_tilemap)
		return false;

	Matrix W = GetTransform()->GetWorldMatrix();
	Matrix V = CUR_SCENE->GetMainCamera()->GetCamera()->GetViewMatrix();
	Matrix P = CUR_SCENE->GetMainCamera()->GetCamera()->GetProjectionMatrix();

	Viewport& vp = GRAPHICS->GetViewport();

	Vec3 n = vp.Unproject(Vec3(screenX, screenY, 0), W, V, P);
	Vec3 f = vp.Unproject(Vec3(screenX, screenY, 1), W, V, P);

	Vec3 start = n;
	Vec3 direction = f - n;
	direction.Normalize();

	Ray ray = Ray(start, direction);

	const auto& vertices = _mesh->GetGeometry()->GetVertices();

	for (int32 z = 0; z < _sizeZ; z++)
	{
		for (int32 x = 0; x < _sizeX; x++)
		{
			uint32 index[4];
			index[0] = (_sizeX + 1) * z + x;
			index[1] = (_sizeX + 1) * z + x + 1;
			index[2] = (_sizeX + 1) * (z + 1) + x;
			index[3] = (_sizeX + 1) * (z + 1) + x + 1;

			Vec3 p[4];
			for (int32 i = 0; i < 4; i++)
				p[i] = vertices[index[i]].position;
				//p[i] = XMVector3TransformCoord(vertices[index[i]].position, W);
				// 월드 좌표 기준으로 연산
			//  [2]
			//   |	\
			//  [0] - [1]
			if (ray.Intersects(p[0], p[1], p[2], OUT distance))
			{
				pickPos = ray.position + ray.direction * distance;
				pickPos = GetTileCorrectedPosition(pickPos);

				// Tilemap에도 갱신
				Vec3 tilePos = _tilemap->ConvertWorldToTile(pickPos);
				Tile* tile = _tilemap->GetTileAt(tilePos);
				DEBUG_LOG("Current Tile POS : [" << tilePos.x << " , " << tilePos.z << "]");


				if (tile)
				{
					//// ✅ 충돌 체크
					//if (_tilemap->CheckCollision(tilePos))
					//	cout << "🚧 Collision Detected!" << endl;

					//// ✅ 스킬 범위 예제 (반경 3)
					//_tilemap->ApplySkillRange(tilePos, 3);

					//// ✅ 전장의 안개 업데이트 (반경 5)
					//_tilemap->UpdateFogOfWar(tilePos, 5);
				}
				
				return true;
			}

			//  [2] - [3]
			//   	\  |
			//		  [1]
			if (ray.Intersects(p[3], p[1], p[2], OUT distance))
			{
				pickPos = ray.position + ray.direction * distance;				
				pickPos = GetTileCorrectedPosition(pickPos);
				
				// Tilemap에도 갱신
				Vec3 tilePos = _tilemap->ConvertWorldToTile(pickPos);
				Tile* tile = _tilemap->GetTileAt(tilePos);

				DEBUG_LOG("Current Tile POS : [" << tilePos.x  << " , " << tilePos.z << "]");



				if (tile)
				{
					//// ✅ 충돌 체크
					//if (_tilemap->CheckCollision(tilePos))
					//	cout << "🚧 Collision Detected!" << endl;

					//// ✅ 스킬 범위 예제 (반경 3)
					//_tilemap->ApplySkillRange(tilePos, 3);

					//// ✅ 전장의 안개 업데이트 (반경 5)
					//_tilemap->UpdateFogOfWar(tilePos, 5);
				}
				return true;				
			}
		}
	}

	return false;
}

Vec3 Terrain::GetTileCorrectedPosition(Vec3 pickPos)
{
	Vec3 tilePos = _tilemap->ConvertWorldToTile(pickPos);
	Tile* tile = _tilemap->GetTileAt(tilePos);

	if (!tile)
		return pickPos; // 타일이 없으면 원래 좌표 반환

	// 🔥 해당 타일의 네 개의 꼭짓점 좌표 가져오기
	int x = static_cast<int>(tilePos.x);
	int z = static_cast<int>(tilePos.z);

	Vec3 p0 = _tilemap->GetTileAt(Vec3(x, 0, z))->position;
	Vec3 p1 = _tilemap->GetTileAt(Vec3(x + 1, 0, z))->position;
	Vec3 p2 = _tilemap->GetTileAt(Vec3(x, 0, z + 1))->position;
	Vec3 p3 = _tilemap->GetTileAt(Vec3(x + 1, 0, z + 1))->position;

	// 🔥 삼각형 판별 (Barycentric 좌표계 사용)
	Vec3 barycentric;
	if (IsPointInTriangle(pickPos, p0, p1, p2, barycentric))
	{
		return BarycentricInterpolation(p0, p1, p2, barycentric);
	}
	else if (IsPointInTriangle(pickPos, p3, p1, p2, barycentric))
	{
		return BarycentricInterpolation(p3, p1, p2, barycentric);
	}

	return pickPos; // 삼각형 판별 실패 시 원래 좌표 반환
}

bool Terrain::IsPointInTriangle(const Vec3& P, const Vec3& A, const Vec3& B, const Vec3& C, Vec3& outBarycentric)
{
	Vec3 v0 = B - A;
	Vec3 v1 = C - A;
	Vec3 v2 = P - A;

	float d00 = v0.Dot(v0);
	float d01 = v0.Dot(v1);
	float d11 = v1.Dot(v1);
	float d20 = v2.Dot(v0);
	float d21 = v2.Dot(v1);

	float denom = d00 * d11 - d01 * d01;
	if (denom == 0.0f) return false;

	float v = (d11 * d20 - d01 * d21) / denom;
	float w = (d00 * d21 - d01 * d20) / denom;
	float u = 1.0f - v - w;

	outBarycentric = Vec3(u, v, w);
	return (u >= 0 && v >= 0 && w >= 0);
}

Vec3 Terrain::BarycentricInterpolation(const Vec3& A, const Vec3& B, const Vec3& C, const Vec3& barycentric)
{
	return A * barycentric.x + B * barycentric.y + C * barycentric.z;
}
