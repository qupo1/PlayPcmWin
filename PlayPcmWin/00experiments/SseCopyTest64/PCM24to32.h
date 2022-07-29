﻿#pragma once

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount 制約なし。
/// @return 処理PCM count。(バイト数ではありません。)
int64_t PCM24to32(const uint8_t *src, int32_t *dst, int64_t pcmCount);
