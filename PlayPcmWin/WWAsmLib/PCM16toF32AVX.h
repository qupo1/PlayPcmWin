#pragma once

// このファイルではなく、PCM16to32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 32 bytes
/// @param pcmCount must be multiply of 16
/// SSE2
extern "C" void PCM16toF32AVX(const int16_t *src, float *dst, int64_t pcmCount);

