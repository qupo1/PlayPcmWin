#pragma once

// このファイルではなく、PCM24to32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 16
extern "C" void PCM24to32Asm(const unsigned char *src, int *dst, int64_t pcmCount);

