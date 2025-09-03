// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "CommonGame/Public/CommonUIExtentions.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeCommonUIExtentions() {}

// Begin Cross Module References
COMMONGAME_API UClass* Z_Construct_UClass_UCommonUIExtentions();
COMMONGAME_API UClass* Z_Construct_UClass_UCommonUIExtentions_NoRegister();
ENGINE_API UClass* Z_Construct_UClass_UBlueprintFunctionLibrary();
UPackage* Z_Construct_UPackage__Script_CommonGame();
// End Cross Module References

// Begin Class UCommonUIExtentions
void UCommonUIExtentions::StaticRegisterNativesUCommonUIExtentions()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UCommonUIExtentions);
UClass* Z_Construct_UClass_UCommonUIExtentions_NoRegister()
{
	return UCommonUIExtentions::StaticClass();
}
struct Z_Construct_UClass_UCommonUIExtentions_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * \n */" },
#endif
		{ "IncludePath", "CommonUIExtentions.h" },
		{ "ModuleRelativePath", "Public/CommonUIExtentions.h" },
	};
#endif // WITH_METADATA
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UCommonUIExtentions>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
UObject* (*const Z_Construct_UClass_UCommonUIExtentions_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UBlueprintFunctionLibrary,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonGame,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonUIExtentions_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UCommonUIExtentions_Statics::ClassParams = {
	&UCommonUIExtentions::StaticClass,
	nullptr,
	&StaticCppClassTypeInfo,
	DependentSingletons,
	nullptr,
	nullptr,
	nullptr,
	UE_ARRAY_COUNT(DependentSingletons),
	0,
	0,
	0,
	0x001000A0u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonUIExtentions_Statics::Class_MetaDataParams), Z_Construct_UClass_UCommonUIExtentions_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UCommonUIExtentions()
{
	if (!Z_Registration_Info_UClass_UCommonUIExtentions.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UCommonUIExtentions.OuterSingleton, Z_Construct_UClass_UCommonUIExtentions_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UCommonUIExtentions.OuterSingleton;
}
template<> COMMONGAME_API UClass* StaticClass<UCommonUIExtentions>()
{
	return UCommonUIExtentions::StaticClass();
}
UCommonUIExtentions::UCommonUIExtentions(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer) {}
DEFINE_VTABLE_PTR_HELPER_CTOR(UCommonUIExtentions);
UCommonUIExtentions::~UCommonUIExtentions() {}
// End Class UCommonUIExtentions

// Begin Registration
struct Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonUIExtentions_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UCommonUIExtentions, UCommonUIExtentions::StaticClass, TEXT("UCommonUIExtentions"), &Z_Registration_Info_UClass_UCommonUIExtentions, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UCommonUIExtentions), 730442221U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonUIExtentions_h_3855384754(TEXT("/Script/CommonGame"),
	Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonUIExtentions_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonUIExtentions_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
