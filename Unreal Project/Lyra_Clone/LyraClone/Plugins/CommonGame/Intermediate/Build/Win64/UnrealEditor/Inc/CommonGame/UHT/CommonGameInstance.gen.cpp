// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "CommonGame/Public/CommonGameInstance.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeCommonGameInstance() {}

// Begin Cross Module References
COMMONGAME_API UClass* Z_Construct_UClass_UCommonGameInstance();
COMMONGAME_API UClass* Z_Construct_UClass_UCommonGameInstance_NoRegister();
ENGINE_API UClass* Z_Construct_UClass_UGameInstance();
UPackage* Z_Construct_UPackage__Script_CommonGame();
// End Cross Module References

// Begin Class UCommonGameInstance
void UCommonGameInstance::StaticRegisterNativesUCommonGameInstance()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UCommonGameInstance);
UClass* Z_Construct_UClass_UCommonGameInstance_NoRegister()
{
	return UCommonGameInstance::StaticClass();
}
struct Z_Construct_UClass_UCommonGameInstance_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * \n */" },
#endif
		{ "IncludePath", "CommonGameInstance.h" },
		{ "ModuleRelativePath", "Public/CommonGameInstance.h" },
	};
#endif // WITH_METADATA
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UCommonGameInstance>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
UObject* (*const Z_Construct_UClass_UCommonGameInstance_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UGameInstance,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonGame,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonGameInstance_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UCommonGameInstance_Statics::ClassParams = {
	&UCommonGameInstance::StaticClass,
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
	0x009000A8u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonGameInstance_Statics::Class_MetaDataParams), Z_Construct_UClass_UCommonGameInstance_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UCommonGameInstance()
{
	if (!Z_Registration_Info_UClass_UCommonGameInstance.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UCommonGameInstance.OuterSingleton, Z_Construct_UClass_UCommonGameInstance_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UCommonGameInstance.OuterSingleton;
}
template<> COMMONGAME_API UClass* StaticClass<UCommonGameInstance>()
{
	return UCommonGameInstance::StaticClass();
}
UCommonGameInstance::UCommonGameInstance(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer) {}
DEFINE_VTABLE_PTR_HELPER_CTOR(UCommonGameInstance);
UCommonGameInstance::~UCommonGameInstance() {}
// End Class UCommonGameInstance

// Begin Registration
struct Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonGameInstance_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UCommonGameInstance, UCommonGameInstance::StaticClass, TEXT("UCommonGameInstance"), &Z_Registration_Info_UClass_UCommonGameInstance, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UCommonGameInstance), 382674483U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonGameInstance_h_2066322040(TEXT("/Script/CommonGame"),
	Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonGameInstance_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_CommonGameInstance_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
