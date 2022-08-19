#pragma once

#include <SDKDDKVer.h>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>

#include "WWNativePcmFmt.h"

class WWPcmFmtConverter {
public:
    HRESULT BitDepthConverter(
        const uint8_t *pcmFrom, const WWNativePcmFmt &fromFmt,
        uint8_t       *pcmTo,   const WWNativePcmFmt &toFmt, const int64_t frameCount);
};
