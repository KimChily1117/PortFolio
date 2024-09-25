#pragma once
#include "ResourceBase.h"


class Texture : public ResourceBase
{
public:
	Texture();
	virtual ~Texture();
public:
	Texture* LoadBmp(HWND hwnd, const wstring& path);
	HDC GetDC();

	void		SetSize(Vec2Int size) { _size = size; }
	Vec2Int		GetSize() { return _size; }

	void SetTransparent(uint32 transparent) { _transparent = transparent; }
	uint32 GetTransparent() { return _transparent; }

	HDC _hdc = {};

private:
	HBITMAP _bitmap = {};
	VectorInt _size = {};
	uint32		_transparent = RGB(255, 0, 255);
};

