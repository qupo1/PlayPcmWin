#pragma once

// このファイルではなく、PCM16to24.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 16
/// SSSE3を使用。
extern "C" void PCM16to24Asm(const int16_t *src, uint8_t *dst, int64_t pcmCount);

