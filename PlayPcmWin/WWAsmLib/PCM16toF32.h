#pragma once

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 32 bytes
/// @param pcmCount 制約なし。
/// @return 処理PCM count。(バイト数ではありません。)
int64_t PCM16toF32(const int16_t *src, float *dst, int64_t pcmCount);
