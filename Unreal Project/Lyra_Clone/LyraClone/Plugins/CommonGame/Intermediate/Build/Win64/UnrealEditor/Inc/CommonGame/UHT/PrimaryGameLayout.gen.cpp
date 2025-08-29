// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "CommonGame/Public/PrimaryGameLayout.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodePrimaryGameLayout() {}

// Begin Cross Module References
COMMONGAME_API UClass* Z_Construct_UClass_UPrimaryGameLayout();
COMMONGAME_API UClass* Z_Construct_UClass_UPrimaryGameLayout_NoRegister();
COMMONUI_API UClass* Z_Construct_UClass_UCommonUserWidget();
UPackage* Z_Construct_UPackage__Script_CommonGame();
// End Cross Module References

// Begin Class UPrimaryGameLayout
void UPrimaryGameLayout::StaticRegisterNativesUPrimaryGameLayout()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UPrimaryGameLayout);
UClass* Z_Construct_UClass_UPrimaryGameLayout_NoRegister()
{
	return UPrimaryGameLayout::StaticClass();
}
struct Z_Construct_UClass_UPrimaryGameLayout_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * \n */" },
#endif
		{ "IncludePath", "PrimaryGameLayout.h" },
		{ "ModuleRelativePath", "Public/PrimaryGameLayout.h" },
	};
#endif // WITH_METADATA
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UPrimaryGameLayout>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
UObject* (*const Z_Construct_UClass_UPrimaryGameLayout_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UCommonUserWidget,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonGame,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UPrimaryGameLayout_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UPrimaryGameLayout_Statics::ClassParams = {
	&UPrimaryGameLayout::StaticClass,
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
	0x00B010A0u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UPrimaryGameLayout_Statics::Class_MetaDataParams), Z_Construct_UClass_UPrimaryGameLayout_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UPrimaryGameLayout()
{
	if (!Z_Registration_Info_UClass_UPrimaryGameLayout.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UPrimaryGameLayout.OuterSingleton, Z_Construct_UClass_UPrimaryGameLayout_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UPrimaryGameLayout.OuterSingleton;
}
template<> COMMONGAME_API UClass* StaticClass<UPrimaryGameLayout>()
{
	return UPrimaryGameLayout::StaticClass();
}
UPrimaryGameLayout::UPrimaryGameLayout(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer) {}
DEFINE_VTABLE_PTR_HELPER_CTOR(UPrimaryGameLayout);
UPrimaryGameLayout::~UPrimaryGameLayout() {}
// End Class UPrimaryGameLayout

// Begin Registration
struct Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_PrimaryGameLayout_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UPrimaryGameLayout, UPrimaryGameLayout::StaticClass, TEXT("UPrimaryGameLayout"), &Z_Registration_Info_UClass_UPrimaryGameLayout, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UPrimaryGameLayout), 320652859U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_PrimaryGameLayout_h_3795136872(TEXT("/Script/CommonGame"),
	Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_PrimaryGameLayout_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_PrimaryGameLayout_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
