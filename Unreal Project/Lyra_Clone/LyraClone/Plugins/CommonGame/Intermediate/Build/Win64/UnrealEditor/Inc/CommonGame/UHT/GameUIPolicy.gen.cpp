// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "CommonGame/Public/GameUIPolicy.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeGameUIPolicy() {}

// Begin Cross Module References
COMMONGAME_API UClass* Z_Construct_UClass_UGameUIPolicy();
COMMONGAME_API UClass* Z_Construct_UClass_UGameUIPolicy_NoRegister();
COREUOBJECT_API UClass* Z_Construct_UClass_UObject();
UPackage* Z_Construct_UPackage__Script_CommonGame();
// End Cross Module References

// Begin Class UGameUIPolicy
void UGameUIPolicy::StaticRegisterNativesUGameUIPolicy()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UGameUIPolicy);
UClass* Z_Construct_UClass_UGameUIPolicy_NoRegister()
{
	return UGameUIPolicy::StaticClass();
}
struct Z_Construct_UClass_UGameUIPolicy_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
		{ "BlueprintType", "true" },
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * \n */" },
#endif
		{ "IncludePath", "GameUIPolicy.h" },
		{ "IsBlueprintBase", "true" },
		{ "ModuleRelativePath", "Public/GameUIPolicy.h" },
	};
#endif // WITH_METADATA
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UGameUIPolicy>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
UObject* (*const Z_Construct_UClass_UGameUIPolicy_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UObject,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonGame,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UGameUIPolicy_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UGameUIPolicy_Statics::ClassParams = {
	&UGameUIPolicy::StaticClass,
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
	0x001000A1u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UGameUIPolicy_Statics::Class_MetaDataParams), Z_Construct_UClass_UGameUIPolicy_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UGameUIPolicy()
{
	if (!Z_Registration_Info_UClass_UGameUIPolicy.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UGameUIPolicy.OuterSingleton, Z_Construct_UClass_UGameUIPolicy_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UGameUIPolicy.OuterSingleton;
}
template<> COMMONGAME_API UClass* StaticClass<UGameUIPolicy>()
{
	return UGameUIPolicy::StaticClass();
}
UGameUIPolicy::UGameUIPolicy(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer) {}
DEFINE_VTABLE_PTR_HELPER_CTOR(UGameUIPolicy);
UGameUIPolicy::~UGameUIPolicy() {}
// End Class UGameUIPolicy

// Begin Registration
struct Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIPolicy_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UGameUIPolicy, UGameUIPolicy::StaticClass, TEXT("UGameUIPolicy"), &Z_Registration_Info_UClass_UGameUIPolicy, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UGameUIPolicy), 2629475379U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIPolicy_h_2038193632(TEXT("/Script/CommonGame"),
	Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIPolicy_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIPolicy_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
