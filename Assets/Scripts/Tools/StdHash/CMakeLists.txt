cmake_minimum_required(VERSION 3.5)
project(StdHash)

add_library(${CMAKE_PROJECT_NAME} SHARED hash.cpp)
install(TARGETS ${CMAKE_PROJECT_NAME} DESTINATION ${CMAKE_CURRENT_SOURCE_DIR}/lib)
