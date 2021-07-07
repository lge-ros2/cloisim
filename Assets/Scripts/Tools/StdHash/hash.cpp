/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#ifndef _NATIVE_LIB_
#define _NATIVE_LIB_

#include <string>

#if __linux__
# define LibraryExport
#else
# define LibraryExport __declspec(dllexport)
#endif

extern "C"
{
  LibraryExport uint64_t GetStringHashCode(const char* string)
  {
    return std::hash<std::string>{}(string);
  }
}
#endif
