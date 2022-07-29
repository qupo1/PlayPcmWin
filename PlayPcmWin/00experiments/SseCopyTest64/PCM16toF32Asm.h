#pragma once

// ���̃t�@�C���ł͂Ȃ��APCM16to32.h���C���N���[�h���Ďg�p���ĉ������B

#include <stdint.h>

/// @param src �A���C���s�v�B
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 8
/// SSE2
extern "C" void PCM16toF32Asm(const int16_t *src, float *dst, int64_t pcmCount);

