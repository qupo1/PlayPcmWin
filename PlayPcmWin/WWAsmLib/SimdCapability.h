#pragma once

#include <stdint.h>

// 64-bit用。

extern "C" {

struct SimdCapability {
    // x64 CPUには、MMX, SSE, SSE2が必ずある。

    uint8_t SSE;
    uint8_t SSE2;
    uint8_t SSE3;
    uint8_t SSSE3;

    uint8_t SSE41;
    uint8_t SSE42;
    uint8_t AVX;
    uint8_t AVX2;

    uint8_t AVXVNNI; //< Alder-Lake (AVX512-VNNIよりも新しい。)
};

struct Avx512Capability {
    uint8_t AVX512F;
    uint8_t AVX512CD;
    uint8_t AVX512ER;
    uint8_t AVX512PF;

    uint8_t AVX5124FMAPS;
    uint8_t AVX5124VNNIW;
    uint8_t AVX512VPOPCNTDQ;
    uint8_t AVX512VL;

    uint8_t AVX512DQ;
    uint8_t AVX512BW;
    uint8_t AVX512IFMA;
    uint8_t AVX512VBMI;

    uint8_t AVX512VBMI2;
    uint8_t AVX512BITALG;
    uint8_t AVX512VNNI; //< Ice-Lake
    uint8_t AVX512BF16;

    uint8_t AVX512VPCLMULQDQ;
    uint8_t AVX512GFNI;
    uint8_t AVX512VAES;
    uint8_t AVX512VP2INTERSECT;
};

}; // extern "C"

void WWGetSimdCapability(SimdCapability *s_return);
void WWGetAvx512Capability(Avx512Capability *s_return);
void WWPrintSimdCapability(SimdCapability *s);
void WWPrintAvx512Capability(Avx512Capability *s);


