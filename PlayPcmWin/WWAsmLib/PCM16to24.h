#pragma once

#include <stdint.h>

/// @param src アライン不要。
/// @param dst must be aligned by 64 bytes
/// @param pcmCount 制約なし。
/// @return 処理PCM count。(バイト数ではありません。)
int64_t PCM16to24(const int16_t *src, uint8_t *dst, int64_t pcmCount);
