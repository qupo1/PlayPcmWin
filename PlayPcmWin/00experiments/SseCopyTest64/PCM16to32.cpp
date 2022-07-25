#include "PCM16to32.h"
#include "PCM16to32Asm.h"

int64_t
PCM16to32(const short *src, int *dst, int64_t pcmCount)
{
    if (pcmCount <= 0) {
        return 0;
    }

    int countRemainder = pcmCount % 8;
    int64_t countAsm = pcmCount - countRemainder;

    PCM16to32Asm(src, dst, countAsm);

    for (int i=0; i<countRemainder; ++i) {
        dst[countAsm+i] = src[countAsm+i] << 16;
    }

    return pcmCount;
}

