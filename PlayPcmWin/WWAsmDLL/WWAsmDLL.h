#pragma once

// C#層とC++層の糊のコードです。

#ifdef WWASMDLL_EXPORTS
#define WWASMDLL_API __declspec(dllexport)
#else
#define WWASMDLL_API __declspec(dllimport)
#endif

#include "SimdCapability.h"

extern "C" {

    WWASMDLL_API int __stdcall
    WWGetCpuCapability(SimdCapability &sc, Avx512Capability &ac);

};
