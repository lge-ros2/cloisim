/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#ifndef _NATIVE_LIB_
#define _NATIVE_LIB_

#include <string>

#if __linux__
# define DllExport
#else
# define DllExport __declspec(dllexport)
#endif

extern "C"
{
  DllExport uint64_t GetStringHashCode(const char* string)
  {
    return std::hash<std::string>{}(std::string(string));
  }
}
#endif
