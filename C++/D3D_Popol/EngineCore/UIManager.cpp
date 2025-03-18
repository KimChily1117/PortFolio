#include "pch.h"
#include "UIManager.h"
#include "Material.h"
#include "Texture.h"
#include "MeshRenderer.h"
#include "../GameCoding2/HUDController.h"
#include "../GameCoding2/SkillIndicatorController.h"
#include "../GameCoding2/CursorController.h"


void UIManager::Init()
{
	_shader = make_shared<Shader>(L"23. RenderDemo.fx");
	_InDecreaseUIShader = make_shared<Shader>(L"24. ImageFillMount.fx");
	
	InitCursor();
	InitHUD();
	InitIndicator();
}

void UIManager::Update()
{

}


// Server에서 받아온 Object정보에서 챔프 타입을 받아와 UI Information을 Set
void UIManager::SetTarget(Protocol::PLAYER_CHAMPION_TYPE type)
{
	switch (type)
	{
	case Protocol::PLAYER_TYPE_NONE:
		break;
	case Protocol::PLAYER_TYPE_ANNIE:
		
		_hud->GetScript<HUDController>()->ChampMark->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"annie_circle"));

		_hud->GetScript<HUDController>()->QSkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"annie_q"));
		_hud->GetScript<HUDController>()->WSkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"annie_w"));
		_hud->GetScript<HUDController>()->ESkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"annie_e"));
		_hud->GetScript<HUDController>()->RSkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"annie_r"));


		break;
	case Protocol::PLAYER_TYPE_GAREN:
		_hud->GetScript<HUDController>()->ChampMark->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"garen_circle"));

		_hud->GetScript<HUDController>()->QSkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"garen_q"));
		_hud->GetScript<HUDController>()->WSkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"garen_w"));
		_hud->GetScript<HUDController>()->ESkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"garen_e"));
		_hud->GetScript<HUDController>()->RSkillSocket->GetMeshRenderer()->SetMaterial(RESOURCES->Get<Material>(L"garen_r"));


		break;
	default:
		break;
	}
}

void UIManager::InitHUD()
{
	// Todo UI Informaition Setting
	// 여기서 필요한 Resources Set
	{ // UI 쪽
			// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"PlayerHUD", L"..\\Resources\\Textures\\UI\\HUD\\PlayerHUD.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"PlayerHUD", material);
		}

		// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"PlayerHUD_Hp", L"..\\Resources\\Textures\\UI\\HUD\\PlayerHUD_Hp.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"PlayerHUD_Hp", material);
		}

		// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"PlayerHUD_Mp", L"..\\Resources\\Textures\\UI\\HUD\\PlayerHUD_Mp.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"PlayerHUD_Mp", material);
		}


		// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"empty_circle", L"..\\Resources\\Textures\\UI\\HUD\\ChampMark\\empty_circle.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"empty_circle", material);
		}


		// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"empty_square	", L"..\\Resources\\Textures\\UI\\HUD\\ChampMark\\empty_square.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"empty_square", material);
		}




		// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"annie_circle", L"..\\Resources\\Textures\\UI\\HUD\\ChampMark\\annie_circle.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"annie_circle", material);
		}

		// Material
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"garen_circle", L"..\\Resources\\Textures\\UI\\HUD\\ChampMark\\garen_circle.png");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"garen_circle", material);
		}

		/// Spell
		
		// Annie
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"annie_q", L"..\\Resources\\Textures\\UI\\Spell\\Annie\\annie_q.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"annie_q", material);
		}
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"annie_w", L"..\\Resources\\Textures\\UI\\Spell\\Annie\\annie_w.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"annie_w", material);
		}
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"annie_e", L"..\\Resources\\Textures\\UI\\Spell\\Annie\\annie_e.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"annie_e", material);
		}
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"annie_r", L"..\\Resources\\Textures\\UI\\Spell\\Annie\\annie_r.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"annie_r", material);
		}
		



		// garen

		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"garen_q", L"..\\Resources\\Textures\\UI\\Spell\\Garen\\garen_q.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"garen_q", material);
		}
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"garen_w", L"..\\Resources\\Textures\\UI\\Spell\\Garen\\garen_w.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"garen_w", material);
		}
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"garen_e", L"..\\Resources\\Textures\\UI\\Spell\\Garen\\garen_e.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"garen_e", material);
		}
		{
			shared_ptr<Material> material = make_shared<Material>();
			material->SetShader(_shader);
			auto texture = RESOURCES->Load<Texture>(L"garen_r", L"..\\Resources\\Textures\\UI\\Spell\\Garen\\garen_r.dds");
			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"garen_r", material);
		}
	}
}

void UIManager::InitCursor()
{
	// Todo UI Cursor Texture Setting
	// 여기서 필요한 Resources Set
	{
		shared_ptr<Material> material = make_shared<Material>();
		material->SetShader(_shader);
		auto texture = RESOURCES->Load<Texture>(L"hover_precise", L"..\\Resources\\Textures\\UI\\Cursor\\hover_precise.tga");
		material->SetDiffuseMap(texture);
		MaterialDesc& desc = material->GetMaterialDesc();
		desc.ambient = Vec4(1.f);
		desc.diffuse = Vec4(1.f);
		desc.specular = Vec4(1.f);
		RESOURCES->Add(L"hover_precise", material);
	}

	{
		shared_ptr<Material> material = make_shared<Material>();
		material->SetShader(_shader);
		auto texture = RESOURCES->Load<Texture>(L"Cursor", L"..\\Resources\\Textures\\UI\\Cursor\\Cursor.png");
		material->SetDiffuseMap(texture);
		MaterialDesc& desc = material->GetMaterialDesc();
		desc.ambient = Vec4(1.f);
		desc.diffuse = Vec4(1.f);
		desc.specular = Vec4(1.f);
		RESOURCES->Add(L"Cursor", material);
	}

	{
		shared_ptr<Material> material = make_shared<Material>();
		material->SetShader(_shader);
		auto texture = RESOURCES->Load<Texture>(L"singletarget", L"..\\Resources\\Textures\\UI\\Cursor\\singletarget.tga");
		material->SetDiffuseMap(texture);
		MaterialDesc& desc = material->GetMaterialDesc();
		desc.ambient = Vec4(1.f);
		desc.diffuse = Vec4(1.f);
		desc.specular = Vec4(1.f);
		RESOURCES->Add(L"singletarget", material);
	}
}

void UIManager::InitIndicator()
{
	// Todo UI Indicator Texture Setting
	// 여기서 필요한 Resources Set
	{
		shared_ptr<Material> material = make_shared<Material>();
		material->SetShader(_shader);
		auto texture = RESOURCES->Load<Texture>(L"aoe", L"..\\Resources\\Textures\\UI\\indicator\\aoe.dds");
		material->SetDiffuseMap(texture);
		MaterialDesc& desc = material->GetMaterialDesc();
		desc.ambient = Vec4(1.f);
		desc.diffuse = Vec4(1.f);
		desc.specular = Vec4(1.f);
		RESOURCES->Add(L"aoe", material);
	}
	{
		shared_ptr<Material> material = make_shared<Material>();
		material->SetShader(_shader);
		auto texture = RESOURCES->Load<Texture>(L"conicrangeindicator", L"..\\Resources\\Textures\\UI\\indicator\\conicrangeindicator.dds");
		material->SetDiffuseMap(texture);
		MaterialDesc& desc = material->GetMaterialDesc();
		desc.ambient = Vec4(1.f);
		desc.diffuse = Vec4(1.f);
		desc.specular = Vec4(1.f);
		RESOURCES->Add(L"conicrangeindicator", material);
	}
	{
		shared_ptr<Material> material = make_shared<Material>();
		material->SetShader(_shader);
		auto texture = RESOURCES->Load<Texture>(L"aoe_brand", L"..\\Resources\\Textures\\UI\\indicator\\aoe_brand.dds");
		material->SetDiffuseMap(texture);
		MaterialDesc& desc = material->GetMaterialDesc();
		desc.ambient = Vec4(1.f);
		desc.diffuse = Vec4(1.f);
		desc.specular = Vec4(1.f);
		RESOURCES->Add(L"aoe_brand", material);
	}

}
