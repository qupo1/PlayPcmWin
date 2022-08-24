#include "PCM24toF32.h"
#include "PCM24toF32Asm.h"
#include "PCM24toF32AVX.h"
#include "PCM24toF32AVX512.h"
#include "SimdCapability.h"

int64_t
PCM24toF32(const uint8_t *src, float *dst, int64_t pcmCount)
{
#ifdef _WIN64
    if (pcmCount <= 0) {
        return 0;
    }

    // ASM実装は16の倍数サンプル単位の処理。端数をC++で処理します。
    int64_t countRemainder = pcmCount % 16;
    int64_t countAsm = pcmCount - countRemainder;

    SimdCapability sc;
    WWGetSimdCapability(&sc);

    Avx512Capability ac;
    WWGetAvx512Capability(&ac);

    if (ac.AVX512F && ac.AVX512BW) {
        PCM24toF32AVX512(src, dst, countAsm);
    } else if (sc.AVX && sc.AVX2) {
        PCM24toF32AVX(src, dst, countAsm);
    } else if (sc.SSSE3) {
        PCM24toF32Asm(src, dst, countAsm);
    } else {
        // 全CPU実行。
        countRemainder = pcmCount;
    }
#else
    int64_t countRemainder = pcmCount;
    int64_t countAsm = 0;
#endif

    for (int i=0; i<countRemainder; ++i) {
        int32_t v = (src[countAsm*3+i*3+2] << 24)
              + (src[countAsm*3+i*3+1] << 16)
              + (src[countAsm*3+i*3+0] << 8);
        dst[countAsm+i] = ((float)v) / (32768.0f * 65536.0f);
    }

    return pcmCount;
}

