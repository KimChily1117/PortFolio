// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "CommonUser/Public/CommonSessionSubsystem.h"
#include "Runtime/Engine/Classes/Engine/GameInstance.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeCommonSessionSubsystem() {}

// Begin Cross Module References
COMMONUSER_API UClass* Z_Construct_UClass_UCommonSession_HostSessionRequest();
COMMONUSER_API UClass* Z_Construct_UClass_UCommonSession_HostSessionRequest_NoRegister();
COMMONUSER_API UClass* Z_Construct_UClass_UCommonSessionSubsystem();
COMMONUSER_API UClass* Z_Construct_UClass_UCommonSessionSubsystem_NoRegister();
COREUOBJECT_API UClass* Z_Construct_UClass_UObject();
COREUOBJECT_API UScriptStruct* Z_Construct_UScriptStruct_FPrimaryAssetId();
ENGINE_API UClass* Z_Construct_UClass_APlayerController_NoRegister();
ENGINE_API UClass* Z_Construct_UClass_UGameInstanceSubsystem();
UPackage* Z_Construct_UPackage__Script_CommonUser();
// End Cross Module References

// Begin Class UCommonSession_HostSessionRequest
void UCommonSession_HostSessionRequest::StaticRegisterNativesUCommonSession_HostSessionRequest()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UCommonSession_HostSessionRequest);
UClass* Z_Construct_UClass_UCommonSession_HostSessionRequest_NoRegister()
{
	return UCommonSession_HostSessionRequest::StaticClass();
}
struct Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
		{ "BlueprintType", "true" },
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * UCommonSession_HostSessionRequest\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd CommonSessionSubsystem \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xcf\xb8\xef\xbf\xbd, CommonSessionSubsystem\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd MapID\xef\xbf\xbd\xef\xbf\xbd CmdArgs\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd CmdArgs\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xce\xb5\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\n */" },
#endif
		{ "IncludePath", "CommonSessionSubsystem.h" },
		{ "ModuleRelativePath", "Public/CommonSessionSubsystem.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "UCommonSession_HostSessionRequest\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd CommonSessionSubsystem \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xcf\xb8\xef\xbf\xbd, CommonSessionSubsystem\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd MapID\xef\xbf\xbd\xef\xbf\xbd CmdArgs\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd CmdArgs\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xce\xb5\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd" },
#endif
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_MapID_MetaData[] = {
		{ "Category", "Session" },
#if !UE_BUILD_SHIPPING
		{ "Comment", "/** \xef\xbf\xbd\xd8\xba\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd MapID (\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd) */" },
#endif
		{ "ModuleRelativePath", "Public/CommonSessionSubsystem.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "\xef\xbf\xbd\xd8\xba\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd MapID (\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd)" },
#endif
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_ExtraArgs_MetaData[] = {
		{ "Category", "Session" },
#if !UE_BUILD_SHIPPING
		{ "Comment", "/** \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd CmdArgs (Experience \xef\xbf\xbd\xcc\xb8\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd) -> \xef\xbf\xbd\xef\xbf\xbd UserFacingExperienceDefinition\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd */" },
#endif
		{ "ModuleRelativePath", "Public/CommonSessionSubsystem.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd CmdArgs (Experience \xef\xbf\xbd\xcc\xb8\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd) -> \xef\xbf\xbd\xef\xbf\xbd UserFacingExperienceDefinition\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd" },
#endif
	};
#endif // WITH_METADATA
	static const UECodeGen_Private::FStructPropertyParams NewProp_MapID;
	static const UECodeGen_Private::FStrPropertyParams NewProp_ExtraArgs_ValueProp;
	static const UECodeGen_Private::FStrPropertyParams NewProp_ExtraArgs_Key_KeyProp;
	static const UECodeGen_Private::FMapPropertyParams NewProp_ExtraArgs;
	static const UECodeGen_Private::FPropertyParamsBase* const PropPointers[];
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UCommonSession_HostSessionRequest>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
const UECodeGen_Private::FStructPropertyParams Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_MapID = { "MapID", nullptr, (EPropertyFlags)0x0010000000000004, UECodeGen_Private::EPropertyGenFlags::Struct, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UCommonSession_HostSessionRequest, MapID), Z_Construct_UScriptStruct_FPrimaryAssetId, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_MapID_MetaData), NewProp_MapID_MetaData) };
const UECodeGen_Private::FStrPropertyParams Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_ExtraArgs_ValueProp = { "ExtraArgs", nullptr, (EPropertyFlags)0x0000000000000000, UECodeGen_Private::EPropertyGenFlags::Str, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, 1, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FStrPropertyParams Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_ExtraArgs_Key_KeyProp = { "ExtraArgs_Key", nullptr, (EPropertyFlags)0x0000000000000000, UECodeGen_Private::EPropertyGenFlags::Str, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, 0, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FMapPropertyParams Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_ExtraArgs = { "ExtraArgs", nullptr, (EPropertyFlags)0x0010000000000004, UECodeGen_Private::EPropertyGenFlags::Map, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UCommonSession_HostSessionRequest, ExtraArgs), EMapPropertyFlags::None, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_ExtraArgs_MetaData), NewProp_ExtraArgs_MetaData) };
const UECodeGen_Private::FPropertyParamsBase* const Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::PropPointers[] = {
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_MapID,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_ExtraArgs_ValueProp,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_ExtraArgs_Key_KeyProp,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::NewProp_ExtraArgs,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::PropPointers) < 2048);
UObject* (*const Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UObject,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonUser,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::ClassParams = {
	&UCommonSession_HostSessionRequest::StaticClass,
	nullptr,
	&StaticCppClassTypeInfo,
	DependentSingletons,
	nullptr,
	Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::PropPointers,
	nullptr,
	UE_ARRAY_COUNT(DependentSingletons),
	0,
	UE_ARRAY_COUNT(Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::PropPointers),
	0,
	0x001000A0u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::Class_MetaDataParams), Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UCommonSession_HostSessionRequest()
{
	if (!Z_Registration_Info_UClass_UCommonSession_HostSessionRequest.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UCommonSession_HostSessionRequest.OuterSingleton, Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UCommonSession_HostSessionRequest.OuterSingleton;
}
template<> COMMONUSER_API UClass* StaticClass<UCommonSession_HostSessionRequest>()
{
	return UCommonSession_HostSessionRequest::StaticClass();
}
UCommonSession_HostSessionRequest::UCommonSession_HostSessionRequest(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer) {}
DEFINE_VTABLE_PTR_HELPER_CTOR(UCommonSession_HostSessionRequest);
UCommonSession_HostSessionRequest::~UCommonSession_HostSessionRequest() {}
// End Class UCommonSession_HostSessionRequest

// Begin Class UCommonSessionSubsystem Function HostSession
struct Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics
{
	struct CommonSessionSubsystem_eventHostSession_Parms
	{
		APlayerController* HostingPlayer;
		UCommonSession_HostSessionRequest* Request;
	};
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Function_MetaDataParams[] = {
		{ "Category", "Session" },
		{ "ModuleRelativePath", "Public/CommonSessionSubsystem.h" },
	};
#endif // WITH_METADATA
	static const UECodeGen_Private::FObjectPropertyParams NewProp_HostingPlayer;
	static const UECodeGen_Private::FObjectPropertyParams NewProp_Request;
	static const UECodeGen_Private::FPropertyParamsBase* const PropPointers[];
	static const UECodeGen_Private::FFunctionParams FuncParams;
};
const UECodeGen_Private::FObjectPropertyParams Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::NewProp_HostingPlayer = { "HostingPlayer", nullptr, (EPropertyFlags)0x0010000000000080, UECodeGen_Private::EPropertyGenFlags::Object, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(CommonSessionSubsystem_eventHostSession_Parms, HostingPlayer), Z_Construct_UClass_APlayerController_NoRegister, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FObjectPropertyParams Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::NewProp_Request = { "Request", nullptr, (EPropertyFlags)0x0010000000000080, UECodeGen_Private::EPropertyGenFlags::Object, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(CommonSessionSubsystem_eventHostSession_Parms, Request), Z_Construct_UClass_UCommonSession_HostSessionRequest_NoRegister, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FPropertyParamsBase* const Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::PropPointers[] = {
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::NewProp_HostingPlayer,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::NewProp_Request,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::PropPointers) < 2048);
const UECodeGen_Private::FFunctionParams Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::FuncParams = { (UObject*(*)())Z_Construct_UClass_UCommonSessionSubsystem, nullptr, "HostSession", nullptr, nullptr, Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::PropPointers, UE_ARRAY_COUNT(Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::PropPointers), sizeof(Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::CommonSessionSubsystem_eventHostSession_Parms), RF_Public|RF_Transient|RF_MarkAsNative, (EFunctionFlags)0x04020401, 0, 0, METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::Function_MetaDataParams), Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::Function_MetaDataParams) };
static_assert(sizeof(Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::CommonSessionSubsystem_eventHostSession_Parms) < MAX_uint16);
UFunction* Z_Construct_UFunction_UCommonSessionSubsystem_HostSession()
{
	static UFunction* ReturnFunction = nullptr;
	if (!ReturnFunction)
	{
		UECodeGen_Private::ConstructUFunction(&ReturnFunction, Z_Construct_UFunction_UCommonSessionSubsystem_HostSession_Statics::FuncParams);
	}
	return ReturnFunction;
}
DEFINE_FUNCTION(UCommonSessionSubsystem::execHostSession)
{
	P_GET_OBJECT(APlayerController,Z_Param_HostingPlayer);
	P_GET_OBJECT(UCommonSession_HostSessionRequest,Z_Param_Request);
	P_FINISH;
	P_NATIVE_BEGIN;
	P_THIS->HostSession(Z_Param_HostingPlayer,Z_Param_Request);
	P_NATIVE_END;
}
// End Class UCommonSessionSubsystem Function HostSession

// Begin Class UCommonSessionSubsystem
void UCommonSessionSubsystem::StaticRegisterNativesUCommonSessionSubsystem()
{
	UClass* Class = UCommonSessionSubsystem::StaticClass();
	static const FNameNativePtrPair Funcs[] = {
		{ "HostSession", &UCommonSessionSubsystem::execHostSession },
	};
	FNativeFunctionRegistrar::RegisterFunctions(Class, Funcs, UE_ARRAY_COUNT(Funcs));
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UCommonSessionSubsystem);
UClass* Z_Construct_UClass_UCommonSessionSubsystem_NoRegister()
{
	return UCommonSessionSubsystem::StaticClass();
}
struct Z_Construct_UClass_UCommonSessionSubsystem_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * \n */" },
#endif
		{ "IncludePath", "CommonSessionSubsystem.h" },
		{ "ModuleRelativePath", "Public/CommonSessionSubsystem.h" },
	};
#endif // WITH_METADATA
	static UObject* (*const DependentSingletons[])();
	static constexpr FClassFunctionLinkInfo FuncInfo[] = {
		{ &Z_Construct_UFunction_UCommonSessionSubsystem_HostSession, "HostSession" }, // 1055449782
	};
	static_assert(UE_ARRAY_COUNT(FuncInfo) < 2048);
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UCommonSessionSubsystem>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
UObject* (*const Z_Construct_UClass_UCommonSessionSubsystem_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UGameInstanceSubsystem,
	(UObject* (*)())Z_Construct_UPackage__Script_CommonUser,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonSessionSubsystem_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UCommonSessionSubsystem_Statics::ClassParams = {
	&UCommonSessionSubsystem::StaticClass,
	nullptr,
	&StaticCppClassTypeInfo,
	DependentSingletons,
	FuncInfo,
	nullptr,
	nullptr,
	UE_ARRAY_COUNT(DependentSingletons),
	UE_ARRAY_COUNT(FuncInfo),
	0,
	0,
	0x001000A0u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UCommonSessionSubsystem_Statics::Class_MetaDataParams), Z_Construct_UClass_UCommonSessionSubsystem_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UCommonSessionSubsystem()
{
	if (!Z_Registration_Info_UClass_UCommonSessionSubsystem.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UCommonSessionSubsystem.OuterSingleton, Z_Construct_UClass_UCommonSessionSubsystem_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UCommonSessionSubsystem.OuterSingleton;
}
template<> COMMONUSER_API UClass* StaticClass<UCommonSessionSubsystem>()
{
	return UCommonSessionSubsystem::StaticClass();
}
DEFINE_VTABLE_PTR_HELPER_CTOR(UCommonSessionSubsystem);
UCommonSessionSubsystem::~UCommonSessionSubsystem() {}
// End Class UCommonSessionSubsystem

// Begin Registration
struct Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UCommonSession_HostSessionRequest, UCommonSession_HostSessionRequest::StaticClass, TEXT("UCommonSession_HostSessionRequest"), &Z_Registration_Info_UClass_UCommonSession_HostSessionRequest, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UCommonSession_HostSessionRequest), 3632853436U) },
		{ Z_Construct_UClass_UCommonSessionSubsystem, UCommonSessionSubsystem::StaticClass, TEXT("UCommonSessionSubsystem"), &Z_Registration_Info_UClass_UCommonSessionSubsystem, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UCommonSessionSubsystem), 2392850748U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_1213925312(TEXT("/Script/CommonUser"),
	Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
