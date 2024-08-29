#include "pch.h"
#include "Game.h"
#include "TimeManager.h"

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

	GET_SINGLE(TimeManager)->Init();

}

void Game::Update()
{

}

void Game::Render()
{
	::Rectangle(_hdc, 200, 200, 400, 400);
}
