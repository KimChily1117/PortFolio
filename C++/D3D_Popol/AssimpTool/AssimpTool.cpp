#include "pch.h"
#include "AssimpTool.h"
#include "Converter.h"

void AssimpTool::Init()
{
	/*{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"DarkKnight2/DarkKnight2_skin1.fbx");
		converter->ExportMaterialData(L"DarkKnight2/DarkKnight2_skin1");
		converter->ExportModelData(L"DarkKnight2/DarkKnight2_skin1");

		converter->ReadAssetFile(L"DarkKnight2/Test.fbx");
		converter->ExportMaterialData(L"DarkKnight2/Test");
		converter->ExportModelData(L"DarkKnight2/Test");
	}*/

	/*///
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Kachujin/Mesh.fbx");
		converter->ExportMaterialData(L"Kachujin/Kachujin/");
		converter->ExportModelData(L"Kachujin/Kachujin");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Kachujin/Idle.fbx");
		converter->ExportAnimationData(L"Kachujin/Idle");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Kachujin/Run.fbx");
		converter->ExportAnimationData(L"Kachujin/Run");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Kachujin/Slash.fbx");
		converter->ExportAnimationData(L"Kachujin/Slash");
	}*/








	// Annie motions(idle, run )
	/*{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Goth_Annie_Idle.fbx");
		converter->ExportAnimationData(L"Annie/Goth_Annie_Idle");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Goth_Annie_Run.fbx");
		converter->ExportAnimationData(L"Annie/Goth_Annie_Run");
	}*/



#pragma region Annie



	/*

	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Mesh.fbx");
		converter->ExportMaterialData(L"Annie/Annie");
		converter->ExportModelData(L"Annie/Annie");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Idle.fbx");
		converter->ExportAnimationData(L"Annie/Idle");
	}

	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Run.fbx");
		converter->ExportAnimationData(L"Annie/Run");
	}*/
	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Annie/Atk1.fbx");
	//	converter->ExportAnimationData(L"Annie/Atk1");
	////}
	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Annie/Atk2.fbx");
	//	converter->ExportAnimationData(L"Annie/Atk2");
	//}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Qspell.fbx");
		converter->ExportAnimationData(L"Annie/Qspell");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Wspell.fbx");
		converter->ExportAnimationData(L"Annie/Wspell");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Espell.fbx");
		converter->ExportAnimationData(L"Annie/Espell");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Rspell.fbx");
		converter->ExportAnimationData(L"Annie/Rspell");
	}
#pragma endregion	 







#pragma region Garen

	/*{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Garen/Mesh.fbx");
		converter->ExportMaterialData(L"Garen/Garen");
		converter->ExportModelData(L"Garen/Garen");
	}
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Garen/Idle.fbx");
		converter->ExportAnimationData(L"Garen/Idle");
	}

	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Garen/Run.fbx");
		converter->ExportAnimationData(L"Garen/Run");
	}*/


	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Garen/Atk1.fbx");
	//	converter->ExportAnimationData(L"Garen/Atk1");
	//}


	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Garen/Atk2.fbx");
	//	converter->ExportAnimationData(L"Garen/Atk2");
	//}


	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Garen/Qspell.fbx");
	//	converter->ExportAnimationData(L"Garen/Qspell");
	//}


	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Garen/Espell.fbx");
	//	converter->ExportAnimationData(L"Garen/Espell");
	//}


	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"Garen/Rspell.fbx");
	//	converter->ExportAnimationData(L"Garen/Rspell");
	//}


#pragma endregion



	////  SummonersRift
	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"SummonersRift/SummonersRift.obj");
	//	converter->ExportMaterialData(L"SummonersRift/SummonersRift");
	//	converter->ExportModelData(L"SummonersRift/SummonersRift");
	//}


	//  SummonersRift
	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"sr/sr.fbx");
	//	converter->ExportMaterialData(L"sr/sr");
	//	converter->ExportModelData(L"sr/sr");
	//}

}

void AssimpTool::Update()
{

}

void AssimpTool::Render()
{
}
