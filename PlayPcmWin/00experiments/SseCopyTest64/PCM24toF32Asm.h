#pragma once

// ���̃t�@�C���ł͂Ȃ��APCM24toF32.h���C���N���[�h���Ďg�p���ĉ������B

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 16
extern "C" void PCM24toF32Asm(const unsigned char *src, float *dst, int64_t pcmCount);

