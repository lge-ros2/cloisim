/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#ifndef _NATIVE_LIB_
#define _NATIVE_LIB_
#include <string>

extern "C"
{
	uint64_t GetStringHashCode(const char* string)
	{
		return std::hash<std::string>{}(string);
	}
}
#endif