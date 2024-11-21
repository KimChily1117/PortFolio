#pragma once

class Game
{
public:
	Game();
	~Game();

public:
	void Init(HWND hwnd);
	void Update();
	void Render();

private:
	HWND _hwnd = {};
	HDC hdc = {};

private:
	// Double Buffering
	RECT _rect;
	HDC hdcBack = {};
	HBITMAP _bmpBack = {};
};

