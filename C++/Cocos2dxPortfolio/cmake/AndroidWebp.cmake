# AGP 4.2's CMake file-api parser rejects OBJECT targets with multiple artifacts.
# Build the same pinned WebP source as ordinary archives on our arm64 target.
# Flags follow upstream Android.mk; no generated HAVE_CONFIG_H is required.
if(NOT ANDROID_ABI STREQUAL "arm64-v8a")
    message(FATAL_ERROR "The Android WebP adapter currently targets arm64-v8a only")
endif()
file(GLOB ASTER_WEBP_SOURCES CONFIGURE_DEPENDS
    "${ASTER_WEBP_ROOT}/src/dec/*.c"
    "${ASTER_WEBP_ROOT}/src/dsp/*.c"
    "${ASTER_WEBP_ROOT}/src/enc/*.c"
    "${ASTER_WEBP_ROOT}/src/utils/*.c")
file(GLOB ASTER_SHARPYUV_SOURCES CONFIGURE_DEPENDS "${ASTER_WEBP_ROOT}/sharpyuv/*.c")
file(GLOB ASTER_WEBP_DEMUX_SOURCES CONFIGURE_DEPENDS "${ASTER_WEBP_ROOT}/src/demux/*.c")
add_library(sharpyuv STATIC ${ASTER_SHARPYUV_SOURCES})
add_library(webp STATIC ${ASTER_WEBP_SOURCES})
add_library(webpdemux STATIC ${ASTER_WEBP_DEMUX_SOURCES})
foreach(ASTER_WEBP_TARGET sharpyuv webp webpdemux)
    target_include_directories(${ASTER_WEBP_TARGET} PRIVATE "${ASTER_WEBP_ROOT}" PUBLIC "${ASTER_WEBP_ROOT}/src")
    target_compile_definitions(${ASTER_WEBP_TARGET} PRIVATE HAVE_MALLOC_H HAVE_PTHREAD WEBP_USE_THREAD)
    set_target_properties(${ASTER_WEBP_TARGET} PROPERTIES POSITION_INDEPENDENT_CODE ON)
endforeach()
target_link_libraries(webp PUBLIC sharpyuv m)
target_link_libraries(webpdemux PUBLIC webp)
