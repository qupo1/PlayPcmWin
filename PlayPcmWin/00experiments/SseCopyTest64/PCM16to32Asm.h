#pragma once

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 8
extern "C" void PCM16to32Asm(const short *src, int *dst, int64_t pcmCount);

