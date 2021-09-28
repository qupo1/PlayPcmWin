#pragma once

#include "WWPcmData.h"

WWPcmData * WWReadWavFile(const wchar_t *path, WWPcmDataStreamAllocType t = WWPDSA_Normal);
