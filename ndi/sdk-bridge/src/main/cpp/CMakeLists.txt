cmake_minimum_required(VERSION 3.22.1)

project(ndi_bridge)

add_library(
    ndi_bridge
    SHARED
    ndi_bridge.cpp
    ndi_screen_share.cpp
)

set_target_properties(
    ndi_bridge
    PROPERTIES
    CXX_STANDARD 17
    CXX_STANDARD_REQUIRED ON
)

find_library(
    log-lib
    log
)

if(DEFINED NDI_SDK_DIR AND EXISTS "${NDI_SDK_DIR}/Include/Processing.NDI.Lib.h")
    message(STATUS "NDI SDK found at: ${NDI_SDK_DIR}")

    target_include_directories(
        ndi_bridge
        PRIVATE
        "${NDI_SDK_DIR}/Include"
    )

    add_library(ndi SHARED IMPORTED)
    set_target_properties(
        ndi
        PROPERTIES
        IMPORTED_LOCATION "${NDI_SDK_DIR}/Lib/${ANDROID_ABI}/libndi.so"
    )

    target_compile_definitions(
        ndi_bridge
        PRIVATE
        NDI_SDK_AVAILABLE=1
    )

    set(NDI_LINK_TARGET ndi)
endif()

target_link_libraries(
    ndi_bridge
    ${log-lib}
    ${NDI_LINK_TARGET}
)

target_compile_definitions(
    ndi_bridge
    PRIVATE
    FEATURE_020_PLAYBACK_QUALITY=1
)
