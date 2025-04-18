#pragma once
#include "ResourceBase.h"
#include "Geometry.h"

class Mesh : public ResourceBase
{
    using Super = ResourceBase;

public:
    Mesh();
    virtual ~Mesh();

	void CreateQuad();
	void CreateParticleQuad();
	void CreateCube();
	void CreateGrid(int32 sizeX, int32 sizeZ);
	void CreateSphere();

	shared_ptr<VertexBuffer> GetVertexBuffer() { return _vertexBuffer; }
	shared_ptr<IndexBuffer> GetIndexBuffer() { return _indexBuffer; }
	shared_ptr<Geometry<VertexTextureNormalTangentData>> GetGeometry() { return _geometry; }


private:
	void CreateBuffers();

private:
	// Mesh
	shared_ptr<Geometry<VertexTextureNormalTangentData>> _geometry;
	shared_ptr<VertexBuffer> _vertexBuffer;
	shared_ptr<IndexBuffer> _indexBuffer;
};

