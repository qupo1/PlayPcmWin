#pragma once

#include <stdint.h>

extern "C" {

struct WWNativePcmFmt {
    int sampleRate;
    int numChannels;
    int validBitDepth;
    int containerBitDepth;
    int isFloat; //< 0: int, 1: float。
    int isDoP; //< DoPの場合、sampleRate=176400 (16分の1), validBitsPerSample=24、containerBitsPerSample={24|32}になります。

    /// 1フレームを収容するバイト数。
    int ContainerBytesPerFrame(void) const {
        return containerBitDepth * numChannels / 8;
    }

    WWNativePcmFmt(void) :
            sampleRate(0),
            numChannels(0),
            validBitDepth(0),
            containerBitDepth(0),
            isFloat(0),
            isDoP(0) {
    }
};

}; // extern "C"

