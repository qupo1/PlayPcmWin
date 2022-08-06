#pragma once

// このファイルではなく、PCM16to24.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 64 bytes
/// @param pcmCount must be multiply of 64
/// AVX512F, AVX512BWを使用。
extern "C" void PCM16to24AVX512(const int16_t *src, uint8_t *dst, int64_t pcmCount);

