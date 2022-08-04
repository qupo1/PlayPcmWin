#pragma once

#include <stdint.h>

// このファイルではなくMyMemcpy2.hをインクルードしてMyMemcpy2を呼んで下さい。


/// @param dst must be 32-byte aligned
/// @param src is not needed to be aligned
/// @param bytes must be multiply of 128
extern "C" void MyMemcpy2AVX(uint8_t * dst, const uint8_t * src, int64_t bytes);

