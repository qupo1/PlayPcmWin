﻿#pragma once

#include <string>

class SimdCapability {
public:
    SimdCapability(void);
    std::string ToString(void);

    // x64 CPUには、MMX, SSE, SSE2が必ずある。

    bool SSE3;
    bool SSSE3;
    bool SSE41;
    bool SSE42;

    bool AVX;
    bool AVX2;
    bool AVXVNNI; //< Alder-Lake (AVX512-VNNIよりも新しい。)
};

class Avx512Capability {
public:
    Avx512Capability(void);
    std::string ToString(void);

    bool AVX512F;
    bool AVX512DQ;
    bool AVX512IFMA;
    bool AVX512CD;

    bool AVX512BW;
    bool AVX512VL;
    bool AVX512VBMI;
    bool AVX512VBMI2;

    bool AVX512GFNI;
    bool AVX512VAES;
    bool AVX512VPCLMULQDQ;
    bool AVX512VNNI; //< Ice-Lake

    bool AVX512BITALG;
    bool AVX512VPOPCNTDQ;
    bool AVX512VP2INTERSECT;
    bool AVX512BF16;
};

