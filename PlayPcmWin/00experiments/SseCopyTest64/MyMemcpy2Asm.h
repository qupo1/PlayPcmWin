﻿#pragma once

#include <stdint.h>

// このファイルではなくMyMemcpy2.hをインクルードしてMyMemcpy2を呼んで下さい。


/// @param dst is not needed to be aligned
/// @param src is not needed to be aligned
/// @param bytes must be multiply of 128
extern "C" void MyMemcpy2Asm(uint8_t * dst, const uint8_t * src, int64_t bytes);

