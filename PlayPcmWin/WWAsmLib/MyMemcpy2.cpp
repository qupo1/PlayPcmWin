#include "MyMemcpy2.h"
#include "MyMemcpy2Asm.h"
#include "MyMemcpy2AVX.h"
#include "MyMemcpy2AVX512.h"
#include <string.h>
#include "SimdCapability.h"
#include <assert.h>

void MyMemcpy2(void *dstV, const void *srcV, int64_t bytes)
{
    SimdCapability sc;
    Avx512Capability ac;

    uint8_t * dst = (uint8_t*)dstV;
    const uint8_t * src = (const uint8_t*)srcV;

    assert((((uint64_t)dst) & 0x63) == 0);

    // ASM������128�̔{���o�C�g�P�ʂ̏����B�[����C++�ŏ������܂��B
    int64_t bytesRemainder = bytes % 128;
    int64_t bytesAsm = bytes - bytesRemainder;

    if (ac.AVX512VL && ac.AVX512F) {
        MyMemcpy2AVX512(dst, src, bytesAsm);
    } else if (sc.AVX) {
        MyMemcpy2AVX(dst, src, bytesAsm);
    } else {
        MyMemcpy2Asm(dst, src, bytesAsm);
    }

    // �c���memcpy�ŃR�s�[���܂��B
    memcpy(dst+bytesAsm, src+bytesAsm, bytesRemainder);
}
