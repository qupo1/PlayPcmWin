#include "MyMemcpy2.h"
#include "MyMemcpy2Asm.h"
#include <string.h>

void MyMemcpy2(uint8_t * dst, const uint8_t * src, int64_t bytes)
{
    // ASM実装は128の倍数バイト単位の処理。端数をC++で処理します。
    int64_t bytesRemainder = bytes % 128;
    int64_t bytesAsm = bytes - bytesRemainder;

    MyMemcpy2Asm(dst, src, bytesAsm);

    // 残りはmemcpyでコピーします。
    memcpy(dst+bytesAsm, src+bytesAsm, bytesRemainder);
}
