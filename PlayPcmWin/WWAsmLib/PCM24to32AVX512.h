#pragma once

// このファイルではなく、PCM24to32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 64 bytes
/// @param pcmCount must be multiply of 16
/// AVX512F, AVX512BW使用。
extern "C" void PCM24to32AVX512(const uint8_t *src, int32_t *dst, int64_t pcmCount);

