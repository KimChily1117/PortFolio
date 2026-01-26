// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeUIExtension_init() {}
	static FPackageRegistrationInfo Z_Registration_Info_UPackage__Script_UIExtension;
	FORCENOINLINE UPackage* Z_Construct_UPackage__Script_UIExtension()
	{
		if (!Z_Registration_Info_UPackage__Script_UIExtension.OuterSingleton)
		{
			static const UECodeGen_Private::FPackageParams PackageParams = {
				"/Script/UIExtension",
				nullptr,
				0,
				PKG_CompiledIn | 0x00000000,
				0xEBD90F48,
				0x6078A200,
				METADATA_PARAMS(0, nullptr)
			};
			UECodeGen_Private::ConstructUPackage(Z_Registration_Info_UPackage__Script_UIExtension.OuterSingleton, PackageParams);
		}
		return Z_Registration_Info_UPackage__Script_UIExtension.OuterSingleton;
	}
	static FRegisterCompiledInInfo Z_CompiledInDeferPackage_UPackage__Script_UIExtension(Z_Construct_UPackage__Script_UIExtension, TEXT("/Script/UIExtension"), Z_Registration_Info_UPackage__Script_UIExtension, CONSTRUCT_RELOAD_VERSION_INFO(FPackageReloadVersionInfo, 0xEBD90F48, 0x6078A200));
PRAGMA_ENABLE_DEPRECATION_WARNINGS
