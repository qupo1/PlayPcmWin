#include "PCM16to24.h"
#include "PCM16to24Asm.h"
#include "PCM16to24AVX.h"
#include "SimdCapability.h"

int64_t
PCM16to24(const int16_t *src, uint8_t *dst, int64_t pcmCount)
{
    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は16の倍数サンプルまたは32の倍数サンプル単位の処理。端数をC++で処理します。
    int64_t countRemainder = pcmCount % 32;
    int64_t countAsm = pcmCount - countRemainder;

    SimdCapability cc;

    if (cc.AVX && cc.AVX2) {
        PCM16to24AVX(src, dst, countAsm);
    } else if (cc.SSSE3) {
        PCM16to24Asm(src, dst, countAsm);
    } else {
        // 全CPU実行。
        countRemainder = pcmCount;
    }

    for (int i=0; i<countRemainder; ++i) {
        uint16_t v = src[countAsm+i];

        dst[(countAsm + i) *3 + 0] = 0;
        dst[(countAsm + i) *3 + 1] = v & 0xff;
        dst[(countAsm + i) *3 + 2] = (v>>8) & 0xff;
    }

    return pcmCount;
}

