#pragma once

#include <stdint.h>

/// @param dst must be 64-byte aligned
/// @param src is not needed to be aligned
/// @param bytes copy bytes
void MyMemcpy2(uint8_t * dst, const uint8_t * src, int64_t bytes);
