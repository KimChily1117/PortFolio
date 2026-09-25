#include "pch.h"
#include "Main.h"
#include "EngineCore/Game.h"
#include "SceneExcuter.h"

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd)
{
#if defined(_DEBUG)
	if (strcmp(lpCmdLine, "--annie-q-smoke") == 0)
	{
		extern int RunAnnieQSmokeTest();
		return RunAnnieQSmokeTest();
	}
#endif
	GameDesc desc;
	desc.appName = L"GameCoding";
	desc.hInstance = hInstance;
	desc.vsync = false;
	desc.hWnd = NULL;
	desc.width = 800;
	desc.height = 600;
	desc.clearColor = Color(0.5f, 0.5f, 0.5f, 0.5f);
	desc.app = make_shared<SceneExcuter>();

	GAME->Run(desc);

	return 0;
}
