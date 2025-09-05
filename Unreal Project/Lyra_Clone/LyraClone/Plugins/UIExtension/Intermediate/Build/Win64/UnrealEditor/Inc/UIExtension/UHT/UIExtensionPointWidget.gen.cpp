// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "UIExtension/Public/UIExtensionPointWidget.h"
#include "Runtime/GameplayTags/Classes/GameplayTagContainer.h"
#include "UIExtension/Public/UIExtensionSystem.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeUIExtensionPointWidget() {}

// Begin Cross Module References
COREUOBJECT_API UClass* Z_Construct_UClass_UClass();
COREUOBJECT_API UClass* Z_Construct_UClass_UObject_NoRegister();
GAMEPLAYTAGS_API UScriptStruct* Z_Construct_UScriptStruct_FGameplayTag();
UIEXTENSION_API UClass* Z_Construct_UClass_UUIExtensionPointWidget();
UIEXTENSION_API UClass* Z_Construct_UClass_UUIExtensionPointWidget_NoRegister();
UIEXTENSION_API UEnum* Z_Construct_UEnum_UIExtension_EUIExtensionPointMatch();
UIEXTENSION_API UScriptStruct* Z_Construct_UScriptStruct_FUIExtensionHandle();
UMG_API UClass* Z_Construct_UClass_UDynamicEntryBoxBase();
UMG_API UClass* Z_Construct_UClass_UUserWidget_NoRegister();
UPackage* Z_Construct_UPackage__Script_UIExtension();
// End Cross Module References

// Begin Class UUIExtensionPointWidget
void UUIExtensionPointWidget::StaticRegisterNativesUUIExtensionPointWidget()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(UUIExtensionPointWidget);
UClass* Z_Construct_UClass_UUIExtensionPointWidget_NoRegister()
{
	return UUIExtensionPointWidget::StaticClass();
}
struct Z_Construct_UClass_UUIExtensionPointWidget_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/**\n * \n */" },
#endif
		{ "IncludePath", "UIExtensionPointWidget.h" },
		{ "ModuleRelativePath", "Public/UIExtensionPointWidget.h" },
		{ "ObjectInitializerConstructorDeclared", "" },
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_ExtensionPointTag_MetaData[] = {
		{ "Category", "UI Extension" },
		{ "ModuleRelativePath", "Public/UIExtensionPointWidget.h" },
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_ExtensionPointTagMatch_MetaData[] = {
		{ "Category", "UI Extension" },
		{ "ModuleRelativePath", "Public/UIExtensionPointWidget.h" },
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_DataClasses_MetaData[] = {
		{ "Category", "UI Extension" },
#if !UE_BUILD_SHIPPING
		{ "Comment", "/** UIExtensionPointWidget\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd UWidget Classes */" },
#endif
		{ "ModuleRelativePath", "Public/UIExtensionPointWidget.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "UIExtensionPointWidget\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd UWidget Classes" },
#endif
	};
	static constexpr UECodeGen_Private::FMetaDataPairParam NewProp_ExtensionMapping_MetaData[] = {
#if !UE_BUILD_SHIPPING
		{ "Comment", "/** UIExtension --- Widget \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd */" },
#endif
		{ "EditInline", "true" },
		{ "ModuleRelativePath", "Public/UIExtensionPointWidget.h" },
#if !UE_BUILD_SHIPPING
		{ "ToolTip", "UIExtension --- Widget \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd \xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd\xef\xbf\xbd" },
#endif
	};
#endif // WITH_METADATA
	static const UECodeGen_Private::FStructPropertyParams NewProp_ExtensionPointTag;
	static const UECodeGen_Private::FBytePropertyParams NewProp_ExtensionPointTagMatch_Underlying;
	static const UECodeGen_Private::FEnumPropertyParams NewProp_ExtensionPointTagMatch;
	static const UECodeGen_Private::FClassPropertyParams NewProp_DataClasses_Inner;
	static const UECodeGen_Private::FArrayPropertyParams NewProp_DataClasses;
	static const UECodeGen_Private::FObjectPropertyParams NewProp_ExtensionMapping_ValueProp;
	static const UECodeGen_Private::FStructPropertyParams NewProp_ExtensionMapping_Key_KeyProp;
	static const UECodeGen_Private::FMapPropertyParams NewProp_ExtensionMapping;
	static const UECodeGen_Private::FPropertyParamsBase* const PropPointers[];
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<UUIExtensionPointWidget>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
const UECodeGen_Private::FStructPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionPointTag = { "ExtensionPointTag", nullptr, (EPropertyFlags)0x0010000000000015, UECodeGen_Private::EPropertyGenFlags::Struct, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UUIExtensionPointWidget, ExtensionPointTag), Z_Construct_UScriptStruct_FGameplayTag, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_ExtensionPointTag_MetaData), NewProp_ExtensionPointTag_MetaData) }; // 1298103297
const UECodeGen_Private::FBytePropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionPointTagMatch_Underlying = { "UnderlyingType", nullptr, (EPropertyFlags)0x0000000000000000, UECodeGen_Private::EPropertyGenFlags::Byte, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, 0, nullptr, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FEnumPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionPointTagMatch = { "ExtensionPointTagMatch", nullptr, (EPropertyFlags)0x0010000000000015, UECodeGen_Private::EPropertyGenFlags::Enum, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UUIExtensionPointWidget, ExtensionPointTagMatch), Z_Construct_UEnum_UIExtension_EUIExtensionPointMatch, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_ExtensionPointTagMatch_MetaData), NewProp_ExtensionPointTagMatch_MetaData) }; // 1937886694
const UECodeGen_Private::FClassPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_DataClasses_Inner = { "DataClasses", nullptr, (EPropertyFlags)0x0104000000000000, UECodeGen_Private::EPropertyGenFlags::Class | UECodeGen_Private::EPropertyGenFlags::ObjectPtr, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, 0, Z_Construct_UClass_UClass, Z_Construct_UClass_UObject_NoRegister, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FArrayPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_DataClasses = { "DataClasses", nullptr, (EPropertyFlags)0x0114000000000015, UECodeGen_Private::EPropertyGenFlags::Array, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UUIExtensionPointWidget, DataClasses), EArrayPropertyFlags::None, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_DataClasses_MetaData), NewProp_DataClasses_MetaData) };
const UECodeGen_Private::FObjectPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionMapping_ValueProp = { "ExtensionMapping", nullptr, (EPropertyFlags)0x0104000000080008, UECodeGen_Private::EPropertyGenFlags::Object | UECodeGen_Private::EPropertyGenFlags::ObjectPtr, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, 1, Z_Construct_UClass_UUserWidget_NoRegister, METADATA_PARAMS(0, nullptr) };
const UECodeGen_Private::FStructPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionMapping_Key_KeyProp = { "ExtensionMapping_Key", nullptr, (EPropertyFlags)0x0100000000080008, UECodeGen_Private::EPropertyGenFlags::Struct, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, 0, Z_Construct_UScriptStruct_FUIExtensionHandle, METADATA_PARAMS(0, nullptr) }; // 1926367713
const UECodeGen_Private::FMapPropertyParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionMapping = { "ExtensionMapping", nullptr, (EPropertyFlags)0x0114008000002008, UECodeGen_Private::EPropertyGenFlags::Map, RF_Public|RF_Transient|RF_MarkAsNative, nullptr, nullptr, 1, STRUCT_OFFSET(UUIExtensionPointWidget, ExtensionMapping), EMapPropertyFlags::None, METADATA_PARAMS(UE_ARRAY_COUNT(NewProp_ExtensionMapping_MetaData), NewProp_ExtensionMapping_MetaData) }; // 1926367713
const UECodeGen_Private::FPropertyParamsBase* const Z_Construct_UClass_UUIExtensionPointWidget_Statics::PropPointers[] = {
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionPointTag,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionPointTagMatch_Underlying,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionPointTagMatch,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_DataClasses_Inner,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_DataClasses,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionMapping_ValueProp,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionMapping_Key_KeyProp,
	(const UECodeGen_Private::FPropertyParamsBase*)&Z_Construct_UClass_UUIExtensionPointWidget_Statics::NewProp_ExtensionMapping,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UUIExtensionPointWidget_Statics::PropPointers) < 2048);
UObject* (*const Z_Construct_UClass_UUIExtensionPointWidget_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_UDynamicEntryBoxBase,
	(UObject* (*)())Z_Construct_UPackage__Script_UIExtension,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_UUIExtensionPointWidget_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_UUIExtensionPointWidget_Statics::ClassParams = {
	&UUIExtensionPointWidget::StaticClass,
	nullptr,
	&StaticCppClassTypeInfo,
	DependentSingletons,
	nullptr,
	Z_Construct_UClass_UUIExtensionPointWidget_Statics::PropPointers,
	nullptr,
	UE_ARRAY_COUNT(DependentSingletons),
	0,
	UE_ARRAY_COUNT(Z_Construct_UClass_UUIExtensionPointWidget_Statics::PropPointers),
	0,
	0x00B000A0u,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_UUIExtensionPointWidget_Statics::Class_MetaDataParams), Z_Construct_UClass_UUIExtensionPointWidget_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_UUIExtensionPointWidget()
{
	if (!Z_Registration_Info_UClass_UUIExtensionPointWidget.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_UUIExtensionPointWidget.OuterSingleton, Z_Construct_UClass_UUIExtensionPointWidget_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_UUIExtensionPointWidget.OuterSingleton;
}
template<> UIEXTENSION_API UClass* StaticClass<UUIExtensionPointWidget>()
{
	return UUIExtensionPointWidget::StaticClass();
}
DEFINE_VTABLE_PTR_HELPER_CTOR(UUIExtensionPointWidget);
UUIExtensionPointWidget::~UUIExtensionPointWidget() {}
// End Class UUIExtensionPointWidget

// Begin Registration
struct Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_UIExtension_Source_UIExtension_Public_UIExtensionPointWidget_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_UUIExtensionPointWidget, UUIExtensionPointWidget::StaticClass, TEXT("UUIExtensionPointWidget"), &Z_Registration_Info_UClass_UUIExtensionPointWidget, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(UUIExtensionPointWidget), 617438281U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_UIExtension_Source_UIExtension_Public_UIExtensionPointWidget_h_1896766895(TEXT("/Script/UIExtension"),
	Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_UIExtension_Source_UIExtension_Public_UIExtensionPointWidget_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_task_Unreal_Project_Lyra_Clone_LyraClone_Plugins_UIExtension_Source_UIExtension_Public_UIExtensionPointWidget_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
