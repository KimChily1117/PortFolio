// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: Enum.proto

#include "Enum.pb.h"

#include <algorithm>

#include <google/protobuf/io/coded_stream.h>
#include <google/protobuf/extension_set.h>
#include <google/protobuf/wire_format_lite.h>
#include <google/protobuf/descriptor.h>
#include <google/protobuf/generated_message_reflection.h>
#include <google/protobuf/reflection_ops.h>
#include <google/protobuf/wire_format.h>
// @@protoc_insertion_point(includes)
#include <google/protobuf/port_def.inc>

PROTOBUF_PRAGMA_INIT_SEG

namespace _pb = ::PROTOBUF_NAMESPACE_ID;
namespace _pbi = _pb::internal;

namespace Protocol {
}  // namespace Protocol
static const ::_pb::EnumDescriptor* file_level_enum_descriptors_Enum_2eproto[3];
static constexpr ::_pb::ServiceDescriptor const** file_level_service_descriptors_Enum_2eproto = nullptr;
const uint32_t TableStruct_Enum_2eproto::offsets[1] = {};
static constexpr ::_pbi::MigrationSchema* schemas = nullptr;
static constexpr ::_pb::Message* const* file_default_instances = nullptr;

const char descriptor_table_protodef_Enum_2eproto[] PROTOBUF_SECTION_VARIABLE(protodesc_cold) =
  "\n\nEnum.proto\022\010Protocol*Z\n\024PLAYER_CHAMPIO"
  "N_TYPE\022\024\n\020PLAYER_TYPE_NONE\020\000\022\025\n\021PLAYER_T"
  "YPE_ANNIE\020\001\022\025\n\021PLAYER_TYPE_GAREN\020\002*^\n\013OB"
  "JECT_TYPE\022\013\n\007UNKNOWN\020\000\022\n\n\006PLAYER\020\001\022\016\n\nAL"
  "LYPLAYER\020\002\022\017\n\013ENEMYPLAYER\020\003\022\n\n\006MINION\020\004\022"
  "\t\n\005TOWER\020\005*h\n\021OBJECT_STATE_TYPE\022\032\n\026OBJEC"
  "T_STATE_TYPE_IDLE\020\000\022\032\n\026OBJECT_STATE_TYPE"
  "_MOVE\020\001\022\033\n\027OBJECT_STATE_TYPE_SKILL\020\002b\006pr"
  "oto3"
  ;
static ::_pbi::once_flag descriptor_table_Enum_2eproto_once;
const ::_pbi::DescriptorTable descriptor_table_Enum_2eproto = {
    false, false, 324, descriptor_table_protodef_Enum_2eproto,
    "Enum.proto",
    &descriptor_table_Enum_2eproto_once, nullptr, 0, 0,
    schemas, file_default_instances, TableStruct_Enum_2eproto::offsets,
    nullptr, file_level_enum_descriptors_Enum_2eproto,
    file_level_service_descriptors_Enum_2eproto,
};
PROTOBUF_ATTRIBUTE_WEAK const ::_pbi::DescriptorTable* descriptor_table_Enum_2eproto_getter() {
  return &descriptor_table_Enum_2eproto;
}

// Force running AddDescriptors() at dynamic initialization time.
PROTOBUF_ATTRIBUTE_INIT_PRIORITY2 static ::_pbi::AddDescriptorsRunner dynamic_init_dummy_Enum_2eproto(&descriptor_table_Enum_2eproto);
namespace Protocol {
const ::PROTOBUF_NAMESPACE_ID::EnumDescriptor* PLAYER_CHAMPION_TYPE_descriptor() {
  ::PROTOBUF_NAMESPACE_ID::internal::AssignDescriptors(&descriptor_table_Enum_2eproto);
  return file_level_enum_descriptors_Enum_2eproto[0];
}
bool PLAYER_CHAMPION_TYPE_IsValid(int value) {
  switch (value) {
    case 0:
    case 1:
    case 2:
      return true;
    default:
      return false;
  }
}

const ::PROTOBUF_NAMESPACE_ID::EnumDescriptor* OBJECT_TYPE_descriptor() {
  ::PROTOBUF_NAMESPACE_ID::internal::AssignDescriptors(&descriptor_table_Enum_2eproto);
  return file_level_enum_descriptors_Enum_2eproto[1];
}
bool OBJECT_TYPE_IsValid(int value) {
  switch (value) {
    case 0:
    case 1:
    case 2:
    case 3:
    case 4:
    case 5:
      return true;
    default:
      return false;
  }
}

const ::PROTOBUF_NAMESPACE_ID::EnumDescriptor* OBJECT_STATE_TYPE_descriptor() {
  ::PROTOBUF_NAMESPACE_ID::internal::AssignDescriptors(&descriptor_table_Enum_2eproto);
  return file_level_enum_descriptors_Enum_2eproto[2];
}
bool OBJECT_STATE_TYPE_IsValid(int value) {
  switch (value) {
    case 0:
    case 1:
    case 2:
      return true;
    default:
      return false;
  }
}


// @@protoc_insertion_point(namespace_scope)
}  // namespace Protocol
PROTOBUF_NAMESPACE_OPEN
PROTOBUF_NAMESPACE_CLOSE

// @@protoc_insertion_point(global_scope)
#include <google/protobuf/port_undef.inc>
