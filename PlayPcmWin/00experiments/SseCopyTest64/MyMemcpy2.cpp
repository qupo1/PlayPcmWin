#include "MyMemcpy2.h"
#include "MyMemcpy2Asm.h"
#include <string.h>

void MyMemcpy2(uint8_t * dst, const uint8_t * src, int64_t bytes)
{
    // ASM������128�̔{���o�C�g�P�ʂ̏����B�[����C++�ŏ������܂��B
    int64_t bytesRemainder = bytes % 128;
    int64_t bytesAsm = bytes - bytesRemainder;

    MyMemcpy2Asm(dst, src, bytesAsm);

    // �c���memcpy�ŃR�s�[���܂��B
    memcpy(dst+bytesAsm, src+bytesAsm, bytesRemainder);
}
