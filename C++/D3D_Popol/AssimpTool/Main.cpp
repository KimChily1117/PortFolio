#include "pch.h"
#include "Main.h"
#include "EngineCore/Game.h"
#include "AssimpTool.h"
#include "StaticMeshDemo.h"
#include "TweenDemo.h"


int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd)
{
	GameDesc desc;
	desc.appName = L"GameCoding";
	desc.hInstance = hInstance;
	desc.vsync = false;
	desc.hWnd = NULL;
	desc.width = 800;
	desc.height = 600;
	desc.clearColor = Color(0.5f, 0.5f, 0.5f, 0.f);
	//desc.app = make_shared<StaticMeshDemo>();
	//desc.app = make_shared<TweenDemo>();
	 
	desc.app = make_shared<AssimpTool>();
	GAME->Run(desc);

	return 0;
}