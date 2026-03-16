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

target_link_libraries(
    ndi_bridge
    ${log-lib}
)
