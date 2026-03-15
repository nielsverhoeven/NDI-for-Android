cmake_minimum_required(VERSION 3.22.1)

project(ndi_bridge)

add_library(
    ndi_bridge
    SHARED
    ndi_bridge.cpp
)

find_library(
    log-lib
    log
)

target_link_libraries(
    ndi_bridge
    ${log-lib}
)
