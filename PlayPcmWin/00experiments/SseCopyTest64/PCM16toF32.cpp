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
        short v = src[countAsm+i];
        float f = ((float)v) * (1.0f / 32768.0f);
        dst[countAsm+i] = f;
    }

    return pcmCount;
}

