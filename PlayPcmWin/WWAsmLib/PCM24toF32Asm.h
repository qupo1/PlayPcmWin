#pragma once

// このファイルではなく、PCM24toF32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 16
extern "C" void PCM24toF32Asm(const uint8_t *src, float *dst, int64_t pcmCount);

