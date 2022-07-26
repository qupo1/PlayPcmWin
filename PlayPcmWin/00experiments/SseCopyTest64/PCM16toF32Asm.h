#pragma once

// ���̃t�@�C���ł͂Ȃ��APCM16to32.h���C���N���[�h���Ďg�p���ĉ������B

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 8
extern "C" void PCM16toF32Asm(const short *src, float *dst, int64_t pcmCount);

