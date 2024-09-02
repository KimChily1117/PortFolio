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
	HWND	_hwnd	= {};
	HDC		_hdc	= {};

	// Double Buffering
private:
	RECT _rect;
	HDC _hdcback = {};
	HBITMAP _bmpback = {};
};

