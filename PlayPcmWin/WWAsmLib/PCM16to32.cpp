#include "PCM16to32.h"
#include "PCM16to32Asm.h"
#include "SimdCapability.h"
#include "PCM16to32AVX.h"
#include "PCM16to32AVX512.h"

int64_t
PCM16to32(const int16_t *src, int32_t *dst, int64_t pcmCount)
{
#ifdef _WIN64
    SimdCapability sc;
    WWGetSimdCapability(&sc);

    Avx512Capability ac;
    WWGetAvx512Capability(&ac);

    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は8の倍数 or 16の倍数 or 32の倍数サンプル単位の処理。端数をC++で処理します。
    int countRemainder = pcmCount % 32;
    int64_t countAsm = pcmCount - countRemainder;

    if (ac.AVX512F && ac.AVX512BW) {
        PCM16to32AVX512(src, dst, countAsm);
    } else if (sc.AVX2) {
        PCM16to32AVX(src, dst, countAsm);
    } else {
        // SSE2実装なので必ず実行できる。
        PCM16to32Asm(src, dst, countAsm);
    }
#else
    int64_t countRemainder = pcmCount;
    int64_t countAsm = 0;
#endif

    for (int i=0; i<countRemainder; ++i) {
        dst[countAsm+i] = src[countAsm+i] << 16;
    }

    return pcmCount;
}

