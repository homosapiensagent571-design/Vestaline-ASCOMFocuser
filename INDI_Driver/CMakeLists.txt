cmake_minimum_required(VERSION 3.13)
PROJECT(indi_vestal_focuser C CXX)

include_directories(${CMAKE_SOURCE_DIR})
include_directories(${CMAKE_SOURCE_DIR}/libs)
include_directories(${CMAKE_SOURCE_DIR}/libs/indibase)

SET(vestal_focuser_SRC
    ${CMAKE_CURRENT_SOURCE_DIR}/vestal_focuser.cpp)

add_executable(indi_vestal_focuser ${vestal_focuser_SRC})
target_link_libraries(indi_vestal_focuser indidriver)
install(TARGETS indi_vestal_focuser RUNTIME DESTINATION bin)

# For standalone build (outside indi source tree)
# Usage:
#   mkdir build && cd build
#   cmake .. -DCMAKE_PREFIX_PATH=/path/to/indi/install
#   make
