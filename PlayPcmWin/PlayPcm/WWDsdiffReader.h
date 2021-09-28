#pragma once

#include "WWPcmData.h"

WWPcmData * WWReadDsdiffFile(const wchar_t *path, WWBitsPerSampleType bitsPerSampleType, WWPcmDataStreamAllocType t = WWPDSA_Normal);
