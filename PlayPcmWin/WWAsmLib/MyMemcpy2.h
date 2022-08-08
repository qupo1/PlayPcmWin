#pragma once

#include <stdint.h>

/// @param dst must be 64byte aligned
/// @param src is not needed to be aligned
/// @param bytes copy bytes
void MyMemcpy2(void * dst, const void * src, int64_t bytes);
