// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

// IWYU pragma: private, include "CommonSessionSubsystem.h"
#include "UObject/ObjectMacros.h"
#include "UObject/ScriptMacros.h"

PRAGMA_DISABLE_DEPRECATION_WARNINGS
class APlayerController;
class UCommonSession_HostSessionRequest;
#ifdef COMMONUSER_CommonSessionSubsystem_generated_h
#error "CommonSessionSubsystem.generated.h already included, missing '#pragma once' in CommonSessionSubsystem.h"
#endif
#define COMMONUSER_CommonSessionSubsystem_generated_h

#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_16_INCLASS_NO_PURE_DECLS \
private: \
	static void StaticRegisterNativesUCommonSession_HostSessionRequest(); \
	friend struct Z_Construct_UClass_UCommonSession_HostSessionRequest_Statics; \
public: \
	DECLARE_CLASS(UCommonSession_HostSessionRequest, UObject, COMPILED_IN_FLAGS(0), CASTCLASS_None, TEXT("/Script/CommonUser"), NO_API) \
	DECLARE_SERIALIZER(UCommonSession_HostSessionRequest)


#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_16_ENHANCED_CONSTRUCTORS \
	/** Standard constructor, called after all reflected properties have been initialized */ \
	NO_API UCommonSession_HostSessionRequest(const FObjectInitializer& ObjectInitializer = FObjectInitializer::Get()); \
private: \
	/** Private move- and copy-constructors, should never be used */ \
	UCommonSession_HostSessionRequest(UCommonSession_HostSessionRequest&&); \
	UCommonSession_HostSessionRequest(const UCommonSession_HostSessionRequest&); \
public: \
	DECLARE_VTABLE_PTR_HELPER_CTOR(NO_API, UCommonSession_HostSessionRequest); \
	DEFINE_VTABLE_PTR_HELPER_CTOR_CALLER(UCommonSession_HostSessionRequest); \
	DEFINE_DEFAULT_OBJECT_INITIALIZER_CONSTRUCTOR_CALL(UCommonSession_HostSessionRequest) \
	NO_API virtual ~UCommonSession_HostSessionRequest();


#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_13_PROLOG
#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_16_GENERATED_BODY \
PRAGMA_DISABLE_DEPRECATION_WARNINGS \
public: \
	FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_16_INCLASS_NO_PURE_DECLS \
	FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_16_ENHANCED_CONSTRUCTORS \
private: \
PRAGMA_ENABLE_DEPRECATION_WARNINGS


template<> COMMONUSER_API UClass* StaticClass<class UCommonSession_HostSessionRequest>();

#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_RPC_WRAPPERS_NO_PURE_DECLS \
	DECLARE_FUNCTION(execHostSession);


#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_INCLASS_NO_PURE_DECLS \
private: \
	static void StaticRegisterNativesUCommonSessionSubsystem(); \
	friend struct Z_Construct_UClass_UCommonSessionSubsystem_Statics; \
public: \
	DECLARE_CLASS(UCommonSessionSubsystem, UGameInstanceSubsystem, COMPILED_IN_FLAGS(0), CASTCLASS_None, TEXT("/Script/CommonUser"), NO_API) \
	DECLARE_SERIALIZER(UCommonSessionSubsystem)


#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_ENHANCED_CONSTRUCTORS \
private: \
	/** Private move- and copy-constructors, should never be used */ \
	UCommonSessionSubsystem(UCommonSessionSubsystem&&); \
	UCommonSessionSubsystem(const UCommonSessionSubsystem&); \
public: \
	DECLARE_VTABLE_PTR_HELPER_CTOR(NO_API, UCommonSessionSubsystem); \
	DEFINE_VTABLE_PTR_HELPER_CTOR_CALLER(UCommonSessionSubsystem); \
	DEFINE_DEFAULT_CONSTRUCTOR_CALL(UCommonSessionSubsystem) \
	NO_API virtual ~UCommonSessionSubsystem();


#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_38_PROLOG
#define FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_GENERATED_BODY \
PRAGMA_DISABLE_DEPRECATION_WARNINGS \
public: \
	FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_RPC_WRAPPERS_NO_PURE_DECLS \
	FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_INCLASS_NO_PURE_DECLS \
	FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h_41_ENHANCED_CONSTRUCTORS \
private: \
PRAGMA_ENABLE_DEPRECATION_WARNINGS


template<> COMMONUSER_API UClass* StaticClass<class UCommonSessionSubsystem>();

#undef CURRENT_FILE_ID
#define CURRENT_FILE_ID FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_CommonUser_Source_CommonUser_Public_CommonSessionSubsystem_h


PRAGMA_ENABLE_DEPRECATION_WARNINGS
