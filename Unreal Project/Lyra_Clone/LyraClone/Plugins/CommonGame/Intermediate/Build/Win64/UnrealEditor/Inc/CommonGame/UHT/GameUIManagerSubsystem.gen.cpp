// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "CommonGame/Public/GameUIManagerSubsystem.h"
#include "Runtime/Engine/Classes/Engine/GameInstance.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeGameUIManagerSubsystem() {}

// Begin Cross Module References
COMMONGAME_API UClass* Z_Construct_UClass_UGameUIManagerSubsystem();
COMMONGAME_API UClass* Z_Construct_UClass_UGameUIManagerSubsystem_NoRegister();
COMMONGAME_API UClass* Z_Construct_UClass_UGameUIPolicy_NoRegister();
ENGINE_API UClass* Z_Construct_UClass_UGameInstanceSubsystem();
UPackage* Z_Construct_UPackage__Script_CommonGame();
// End Cross Module References

// Begin Class UGameUIManagerSubsystem
void UGameUIManagerSubsystem::StaticRegisterNativesUGameUIManagerSubsystem()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UGameUIManagerSubsystem);
UClass* Z_Construct_UClass_UGameUIManagerSubsystem_NoRegister()
{
	return UGameUIManagerSubsystem::StaticClass();
}
struct Z_Construct_UClass_UGameUIManagerSubsystem_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * GameUIManagerSubsystem\xef\xbf\xbd\xef\xbf\xbd GameInstanceSubsystem\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xd1\xb4\xef\xbf\xbd\n * - \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xe2\xbc\xad \xef\xbf\xbd\xd6\xb8\xef\xbf\xbd\xef\xbf\xbd\xd8\xbe\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd UGameUIManagerSubsystem\xef\xbf\xbd\xef\xbf\xbd UCLASS \xef\xbf\xbd\xd3\xbc\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd Abstract\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xc7\xb5\xc7\xbe\xef\xbf\xbd \xef\xbf\xbd\xd6\xb4\xef\xbf\xbd\n * - \xef\xbf\xbd\xd8\xb4\xef\xbf\xbd \xc5\xac\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xdc\xb5\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xd2\xb0\xef\xbf\xbd\xef\xbf\xbd\xcf\xb8\xef\xbf\xbd, \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xd3\xb9\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd Concrete Class\xef\xbf\xbd\xce\xbc\xef\xbf\xbd \xc8\xb0\xef\xbf\xbd\xef\xbf\xbd\xc7\xbe\xef\xbf\xbd \xef\xbf\xbd\xd1\xb4\xef\xbf\xbd\n */" },
#endif
		{ "IncludePath", "GameUIManagerSubsystem.h" },
		{ "ModuleRelativePath", "Public/GameUIManagerSubsystem.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "GameUIManagerSubsystem\xef\xbf\xbd\xef\xbf\xbd GameInstanceSubsystem\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xd1\xb4\xef\xbf\xbd\n- \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xe2\xbc\xad \xef\xbf\xbd\xd6\xb8\xef\xbf\xbd\xef\xbf\xbd\xd8\xbe\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd UGameUIManagerSubsystem\xef\xbf\xbd\xef\xbf\xbd UCLASS \xef\xbf\xbd\xd3\xbc\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd Abstract\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xc7\xb5\xc7\xbe\xef\xbf\xbd \xef\xbf\xbd\xd6\xb4\xef\xbf\xbd\n- \xef\xbf\xbd\xd8\xb4\xef\xbf\xbd \xc5\xac\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xdc\xb5\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xd2\xb0\xef\xbf\xbd\xef\xbf\xbd\xcf\xb8\xef\xbf\xbd, \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xd3\xb9\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd Concrete Class\xef\xbf\xbd\xce\xbc\xef\xbf\xbd \xc8\xb0\xef\xbf\xbd\xef\xbf\xbd\xc7\xbe\xef\xbf\xbd \xef\xbf\xbd\xd1\xb4\xef\xbf\xbd" },
#endif
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_CurrentPolicy_MetaData[] = {
		{ "ModuleRelativePath", "Public/GameUIManagerSubsystem.h" },
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_DefaultUIPolicyClass_MetaData[] = {
		{ "Category", "GameUIManagerSubsystem" },
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n\x09 * default UI policy\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd classs\n\x09 * - \xef\xbf\xbd\xec\xb8\xae\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xd8\xb4\xef\xbf\xbd \xc5\xac\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd B_BttGameUIPolicy\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xcc\xb4\xef\xbf\xbd\n\x09 */" },
#endif
		{ "ModuleRelativePath", "Public/GameUIManagerSubsystem.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "default UI policy\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd classs\n- \xef\xbf\xbd\xec\xb8\xae\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xd8\xb4\xef\xbf\xbd \xc5\xac\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd B_BttGameUIPolicy\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xcc\xb4\xef\xbf\xbd" },
#endif
	};
#endif // WITH_METADATA
	static const UECodeGen_Private::FObjectPropertyParams NewProp_CurrentPolicy;
	static const UECodeGen_Private::FSoftClassPropertyParams NewProp_DefaultUIPolicyClass;
	static const UECodeGen_Private::FPropertyParamsBase* const PropPointers[];
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UGameUIManagerSubsystem>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
const UECodeGen_Private::FObjectPropertyParams Z_Construct_UClass_UGameUIManagerSubsystem_Statics::NewProp_CurrentPolicy = { "CurrentPolicy", nullptr, (EPropertyFlags)0x0114000000002000, UECodeGen_Private::EPropertyGenFlags::Object | UECodeGen_Private::EPropertyGenFlags::ObjectPtr, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UGameUIManagerSubsystem, CurrentPolicy), Z_Construct_UClass_UGameUIPolicy_NoRegister, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_CurrentPolicy_MetaData), NewProp_CurrentPolicy_MetaData) };
const UECodeGen_Private::FSoftClassPropertyParams Z_Construct_UClass_UGameUIManagerSubsystem_Statics::NewProp_DefaultUIPolicyClass = { "DefaultUIPolicyClass", nullptr, (EPropertyFlags)0x0014000000004001, UECodeGen_Private::EPropertyGenFlags::SoftClass, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UGameUIManagerSubsystem, DefaultUIPolicyClass), Z_Construct_UClass_UGameUIPolicy_NoRegister, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_DefaultUIPolicyClass_MetaData), NewProp_DefaultUIPolicyClass_MetaData) };
const UECodeGen_Private::FPropertyParamsBase* const Z_Construct_UClass_UGameUIManagerSubsystem_Statics::PropPointers[] = {
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UGameUIManagerSubsystem_Statics::NewProp_CurrentPolicy,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UGameUIManagerSubsystem_Statics::NewProp_DefaultUIPolicyClass,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UGameUIManagerSubsystem_Statics::PropPointers) < 2048);
UObject* (*const Z_Construct_UClass_UGameUIManagerSubsystem_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UGameInstanceSubsystem,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonGame,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UGameUIManagerSubsystem_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UGameUIManagerSubsystem_Statics::ClassParams = {
	&UGameUIManagerSubsystem::StaticClass,
	"Game",
	&StaticCppClassTypeInfo,
	DependentSingletons,
	nullptr,
	Z_Construct_UClass_UGameUIManagerSubsystem_Statics::PropPointers,
	nullptr,
	UE_ARRAY_COUNT(DependentSingletons),
	0,
	UE_ARRAY_COUNT(Z_Construct_UClass_UGameUIManagerSubsystem_Statics::PropPointers),
	0,
	0x001000A5u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UGameUIManagerSubsystem_Statics::Class_MetaDataParams), Z_Construct_UClass_UGameUIManagerSubsystem_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UGameUIManagerSubsystem()
{
	if (!Z_Registration_Info_UClass_UGameUIManagerSubsystem.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UGameUIManagerSubsystem.OuterSingleton, Z_Construct_UClass_UGameUIManagerSubsystem_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UGameUIManagerSubsystem.OuterSingleton;
}
template<> COMMONGAME_API UClass* StaticClass<UGameUIManagerSubsystem>()
{
	return UGameUIManagerSubsystem::StaticClass();
}
DEFINE_VTABLE_PTR_HELPER_CTOR(UGameUIManagerSubsystem);
UGameUIManagerSubsystem::~UGameUIManagerSubsystem() {}
// End Class UGameUIManagerSubsystem

// Begin Registration
struct Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIManagerSubsystem_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UGameUIManagerSubsystem, UGameUIManagerSubsystem::StaticClass, TEXT("UGameUIManagerSubsystem"), &Z_Registration_Info_UClass_UGameUIManagerSubsystem, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UGameUIManagerSubsystem), 4115395871U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIManagerSubsystem_h_2431735204(TEXT("/Script/CommonGame"),
	Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIManagerSubsystem_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonGame_Source_CommonGame_Public_GameUIManagerSubsystem_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
