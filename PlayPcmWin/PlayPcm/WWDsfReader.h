#pragma once

#include "WWPcmData.h"

WWPcmData * WWReadDsfFile(const wchar_t *path, WWBitsPerSampleType bitsPerSampleType, WWPcmDataStreamAllocType t = WWPDSA_Normal);
