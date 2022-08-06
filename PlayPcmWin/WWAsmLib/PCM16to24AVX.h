#pragma once

// このファイルではなく、PCM16to24.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 32
/// SSSE3を使用。
extern "C" void PCM16to24AVX(const int16_t *src, uint8_t *dst, int64_t pcmCount);

