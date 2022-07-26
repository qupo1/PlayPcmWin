#include "PCM16toF32.h"
#include "PCM16toF32Asm.h"

int64_t
PCM16toF32(const short *src, float *dst, int64_t pcmCount)
{
    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は8の倍数サンプル単位の処理。端数をC++で処理します。
    int countRemainder = pcmCount % 8;
    int64_t countAsm = pcmCount - countRemainder;

    PCM16toF32Asm(src, dst, countAsm);

    for (int i=0; i<countRemainder; ++i) {
        int v = src[countAsm+i] << 16;
        float f = ((float)v) * (1.0f / 0x80000000);
        dst[countAsm+i] = f;
    }

    return pcmCount;
}

