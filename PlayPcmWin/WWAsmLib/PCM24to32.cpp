#include "PCM24to32.h"
#include "PCM24to32Asm.h"
#include "PCM24to32AVX.h"
#include "SimdCapability.h"

int64_t
PCM24to32(const uint8_t *src, int32_t *dst, int64_t pcmCount)
{
    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は16の倍数サンプル単位の処理。端数をC++で処理します。
    int64_t countRemainder = pcmCount % 16;
    int64_t countAsm = pcmCount - countRemainder;

    SimdCapability cc;

    if (cc.AVX && cc.AVX2) {
        PCM24to32AVX(src, dst, countAsm);
    } else if (cc.SSSE3) {
        PCM24to32Asm(src, dst, countAsm);
    } else {
        // 全CPU実行。
        countRemainder = pcmCount;
    }

    for (int i=0; i<countRemainder; ++i) {
        dst[countAsm+i] = (src[countAsm*3+i*3+2] << 24)
                        + (src[countAsm*3+i*3+1] << 16)
                        + (src[countAsm*3+i*3+0] << 8);
    }

    return pcmCount;
}

