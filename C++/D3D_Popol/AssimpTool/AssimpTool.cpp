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

	
	
	
	
	
	//  Annie
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Goth_Annie.fbx");
		converter->ExportMaterialData(L"Annie/Goth_Annie");
		converter->ExportModelData(L"Annie/Goth_Annie");		
	}
	
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
	{
		shared_ptr<Converter> converter = make_shared<Converter>();
		converter->ReadAssetFile(L"Annie/Idle.fbx");
		converter->ExportAnimationData(L"Annie/Idle");
	}


	 
	 
	 
	 
	 
	////  SummonersRift
	//{
	//	shared_ptr<Converter> converter = make_shared<Converter>();
	//	converter->ReadAssetFile(L"SummonersRift/SummonersRift.obj");
	//	converter->ExportMaterialData(L"SummonersRift/SummonersRift");
	//	converter->ExportModelData(L"SummonersRift/SummonersRift");
	//}
	 
	 
	////  SummonersRift
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
