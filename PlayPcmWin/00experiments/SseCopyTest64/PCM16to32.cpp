﻿#include "PCM16to32.h"
#include "PCM16to32Asm.h"

int64_t
PCM16to32(const int16_t *src, int32_t *dst, int64_t pcmCount)
{
    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は8の倍数サンプル単位の処理。端数をC++で処理します。
    int countRemainder = pcmCount % 8;
    int64_t countAsm = pcmCount - countRemainder;

    // SSE2実装なので必ず実行できる。
    PCM16to32Asm(src, dst, countAsm);

    for (int i=0; i<countRemainder; ++i) {
        dst[countAsm+i] = src[countAsm+i] << 16;
    }

    return pcmCount;
}

