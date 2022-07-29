#pragma once

// このファイルではなく、PCM16to32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 8
/// Uses SSE2
extern "C" void PCM16to32Asm(const int16_t *src, int32_t *dst, int64_t pcmCount);

