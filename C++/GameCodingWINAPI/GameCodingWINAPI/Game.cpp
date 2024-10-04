#include "pch.h"
#include "Game.h"
#include "TimeManager.h"
#include "InputManager.h"
#include "SceneManager.h"
#include "ResourceManager.h"

Game::Game()
{

}

Game::~Game()
{

}

void Game::Init(HWND hwnd)
{
	_hwnd = hwnd;
	_hdc = ::GetDC(_hwnd);

	::GetClientRect(hwnd, &_rect);

	// 현재 사용하고있는 hdc와 호환뇌는 DC를 만들어줌
	_hdcback = ::CreateCompatibleDC(_hdc);
	_bmpback = ::CreateCompatibleBitmap(_hdc, _rect.right, _rect.bottom);
	HBITMAP prev = (HBITMAP)::SelectObject(_hdcback, _bmpback);
	::DeleteObject(prev);


	GET_SINGLE(TimeManager)->Init();
	GET_SINGLE(InputManager)->Init(hwnd);
	GET_SINGLE(SceneManager)->Init();
	GET_SINGLE(ResourceManager)->Init(hwnd, fs::path(L"../Resources"));
	GET_SINGLE(SceneManager)->ChangeScene(SceneType::DevScene);


}

void Game::Update()
{
	GET_SINGLE(TimeManager)->Update();
	GET_SINGLE(InputManager)->Update();
	GET_SINGLE(SceneManager)->Update();
}

void Game::Render()
{
	GET_SINGLE(SceneManager)->Render(_hdcback);
	uint32 fps = GET_SINGLE(TimeManager)->GetFps();
	float deltaTime = GET_SINGLE(TimeManager)->GetDeltaTime();

	{
		POINT mousePos = GET_SINGLE(InputManager)->GetMousePos();

		wstring str = format(L"MOUSEPOS X : ({0}) , Y : ({1})", mousePos.x, mousePos.y);

		TextOut(_hdcback, 20, 10, str.c_str(), static_cast<int32>(str.size()));
	}

	wstring str = format(L"FPS({0}) , DT({1})ms", fps, static_cast<int32>(deltaTime));
	TextOut(_hdcback, 650, 10, str.c_str(), static_cast<int32>(str.size()));

	::BitBlt(_hdc, 0, 0, _rect.right, _rect.bottom, _hdcback, 0, 0, SRCCOPY);
	::PatBlt(_hdcback, 0, 0, _rect.right, _rect.bottom, WHITENESS);





}
