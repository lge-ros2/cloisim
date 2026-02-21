# generated from ament/cmake/core/templates/nameConfig.cmake.in

# prevent multiple inclusion
if(_cloisim_ros2_native_CONFIG_INCLUDED)
  # ensure to keep the found flag the same
  if(NOT DEFINED cloisim_ros2_native_FOUND)
    # explicitly set it to FALSE, otherwise CMake will set it to TRUE
    set(cloisim_ros2_native_FOUND FALSE)
  elseif(NOT cloisim_ros2_native_FOUND)
    # use separate condition to avoid uninitialized variable warning
    set(cloisim_ros2_native_FOUND FALSE)
  endif()
  return()
endif()
set(_cloisim_ros2_native_CONFIG_INCLUDED TRUE)

# output package information
if(NOT cloisim_ros2_native_FIND_QUIETLY)
  message(STATUS "Found cloisim_ros2_native: 1.0.0 (${cloisim_ros2_native_DIR})")
endif()

# warn when using a deprecated package
if(NOT "" STREQUAL "")
  set(_msg "Package 'cloisim_ros2_native' is deprecated")
  # append custom deprecation text if available
  if(NOT "" STREQUAL "TRUE")
    set(_msg "${_msg} ()")
  endif()
  # optionally quiet the deprecation message
  if(NOT cloisim_ros2_native_DEPRECATED_QUIET)
    message(DEPRECATION "${_msg}")
  endif()
endif()

# flag package as ament-based to distinguish it after being find_package()-ed
set(cloisim_ros2_native_FOUND_AMENT_PACKAGE TRUE)

# include all config extra files
set(_extras "ament_cmake_export_libraries-extras.cmake;ament_cmake_export_dependencies-extras.cmake")
foreach(_extra ${_extras})
  include("${cloisim_ros2_native_DIR}/${_extra}")
endforeach()
