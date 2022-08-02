#include "MyMemcpy2.h"
#include "MyMemcpy2Asm.h"
#include "MyMemcpy2AVX.h"
#include "MyMemcpy2AVX512.h"
#include <string.h>
#include "SimdCapability.h"
#include <assert.h>

void MyMemcpy2(uint8_t * dst, const uint8_t * src, int64_t bytes)
{
    SimdCapability sc;
    Avx512Capability ac;

    assert((uint64_t)dst & 0x63 == 0);

    // ASM実装は128の倍数バイト単位の処理。端数をC++で処理します。
    int64_t bytesRemainder = bytes % 128;
    int64_t bytesAsm = bytes - bytesRemainder;

    if (ac.AVX512VL && ac.AVX512F) {
        MyMemcpy2AVX512(dst, src, bytesAsm);
    } else if (sc.AVX) {
        MyMemcpy2AVX(dst, src, bytesAsm);
    } else {
        MyMemcpy2Asm(dst, src, bytesAsm);
    }

    // 残りはmemcpyでコピーします。
    memcpy(dst+bytesAsm, src+bytesAsm, bytesRemainder);
}
