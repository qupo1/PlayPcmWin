﻿#include "PCM16toF32.h"
#include "PCM16toF32Asm.h"
#include "PCM16toF32AVX.h"
#include "PCM16toF32AVX512.h"
#include "SimdCapability.h"

int64_t
PCM16toF32(const int16_t *src, float *dst, int64_t pcmCount)
{
#ifdef _WIN64
    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は8か16の倍数サンプル単位の処理。端数をC++で処理します。
    int countRemainder = pcmCount % 16;
    int64_t countAsm = pcmCount - countRemainder;

    SimdCapability sc;
    Avx512Capability ac;

    if (ac.AVX512F && ac.AVX512BW) {
        PCM16toF32AVX512(src, dst, countAsm);
    } else if (sc.AVX && sc.AVX2) {
        PCM16toF32AVX(src, dst, countAsm);
    } else {
        // SSE2実装なので必ず実行できる。
        PCM16toF32Asm(src, dst, countAsm);
    }
#else
    int64_t countRemainder = pcmCount;
    int64_t countAsm = 0;
#endif

    for (int i=0; i<countRemainder; ++i) {
        int16_t v = src[countAsm+i];
        float f = ((float)v) * (1.0f / 32768.0f);
        dst[countAsm+i] = f;
    }

    return pcmCount;
}

