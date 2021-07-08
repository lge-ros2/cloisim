cmake_minimum_required(VERSION 3.5)
project(StdHash)

set(CMAKE_CXX_STANDARD 14)
add_compile_options(-Wall -Wextra -Wpedantic -Wdeprecated -fPIC)
add_compile_options(-static-libstdc++ -static-libgcc -O3)

add_library(${CMAKE_PROJECT_NAME} SHARED hash.cpp)

set(lib_target linux)
if (CMAKE_CXX_COMPILER MATCHES "mingw32")
	#message(STATUS "Build DLL library")
	set_target_properties(${CMAKE_PROJECT_NAME} PROPERTIES SUFFIX ".dll")
	set_target_properties(${CMAKE_PROJECT_NAME} PROPERTIES PREFIX "")
	set(lib_target windows)
endif()

install(
	TARGETS ${CMAKE_PROJECT_NAME}
	DESTINATION ${CMAKE_CURRENT_SOURCE_DIR}/lib/${lib_target})
