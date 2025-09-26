// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeCommonGame_init() {}
	static FPackageRegistrationInfo Z_Registration_Info_UPackage__Script_CommonGame;
	FORCENOINLINE UPackage* Z_Construct_UPackage__Script_CommonGame()
	{
		if (!Z_Registration_Info_UPackage__Script_CommonGame.OuterSingleton)
		{
			static const UECodeGen_Private::FPackageParams PackageParams = {
				"/Script/CommonGame",
				nullptr,
				0,
				PKG_CompiledIn | 0x00000000,
				0xC68AA770,
				0x9CB33E0D,
				METADATA_PARAMS(0, nullptr)
			};
			UECodeGen_Private::ConstructUPackage(Z_Registration_Info_UPackage__Script_CommonGame.OuterSingleton, PackageParams);
		}
		return Z_Registration_Info_UPackage__Script_CommonGame.OuterSingleton;
	}
	static FRegisterCompiledInInfo Z_CompiledInDeferPackage_UPackage__Script_CommonGame(Z_Construct_UPackage__Script_CommonGame, TEXT("/Script/CommonGame"), Z_Registration_Info_UPackage__Script_CommonGame, CONSTRUCT_RELOAD_VERSION_INFO(FPackageReloadVersionInfo, 0xC68AA770, 0x9CB33E0D));
PRAGMA_ENABLE_DEPRECATION_WARNINGS
