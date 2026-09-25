#include "pch.h"
#include "Main.h"
#include "EngineCore/Game.h"
#include "AssimpTool.h"
#include "StaticMeshDemo.h"
#include "TweenDemo.h"
#include "Converter.h"
#include <shellapi.h>
#pragma comment(lib, "shell32.lib")


int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd)
{
	int argc = 0;
	LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
	if (argv && argc > 1 && wcscmp(argv[1], L"--convert-scb-vfx") == 0)
	{
		// No window, D3D device, network, or game loop is needed for baking.
		const bool keepAxes = argc == 5 && wcscmp(argv[4], L"--keep-axes") == 0;
		const bool ok = (argc == 4 || keepAxes) && Converter::ExportScbVfxMesh(argv[2], argv[3], keepAxes);
		LocalFree(argv);
		return ok ? 0 : 1;
	}
	if (argv) LocalFree(argv);
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
