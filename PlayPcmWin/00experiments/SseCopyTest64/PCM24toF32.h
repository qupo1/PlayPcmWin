﻿#pragma once

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount 制約なし。
/// @return 処理PCM count。(バイト数ではありません。)
int64_t PCM24toF32(const unsigned char *src, float *dst, int64_t pcmCount);
