#pragma once

// このファイルではなく、PCM16to32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 8
extern "C" void PCM16toF32Asm(const short *src, float *dst, int64_t pcmCount);

