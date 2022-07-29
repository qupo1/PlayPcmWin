#pragma once

// ���̃t�@�C���ł͂Ȃ��APCM16to24.h���C���N���[�h���Ďg�p���ĉ������B

#include <stdint.h>

/// @param src must be aligned by 16 bytes
/// @param dst must be aligned by 16 bytes
/// @param pcmCount must be multiply of 16
/// SSSE3���g�p�B
extern "C" void PCM16to24Asm(const int16_t *src, uint8_t *dst, int64_t pcmCount);

