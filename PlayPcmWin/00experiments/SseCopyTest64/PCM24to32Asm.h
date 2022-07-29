#pragma once

// このファイルではなく、PCM24to32.hをインクルードして使用して下さい。

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 16
/// SSSE3使用。
extern "C" void PCM24to32Asm(const uint8_t *src, int32_t *dst, int64_t pcmCount);

